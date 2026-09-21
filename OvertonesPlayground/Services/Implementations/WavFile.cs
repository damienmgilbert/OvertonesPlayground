namespace OvertonesPlayground.Services.Implementations;

///<summary>
///Minimal reader/writer for uncompressed 16-bit PCM WAV files - just enough to power the
///trim/gain/fade/reverse/normalize tools in <see cref="AudioEditorService"/> without pulling in a full audio codec
///library.
///</summary>
internal sealed class WavFile
{
    #region Constants
    ///<summary>
    ///The only audio format code this reader accepts (uncompressed PCM); anything else (e.g. compressed or
    ///IEEE-float WAV) is rejected rather than silently misinterpreted as raw PCM.
    ///</summary>
    private const short PcmAudioFormat = 1;

    ///<summary>
    ///The only sample depth this reader/writer supports.
    ///</summary>
    private const short SupportedBitsPerSample = 16;
    #endregion

    #region Private methods
    ///<summary>
    ///The synchronous body of <see cref="ReadAsync"/>.
    ///</summary>
    private static WavFile Read(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using BinaryReader reader = new(stream);

        try
        {
            bool isNotRiff = new string(reader.ReadChars(4)) != "RIFF";
            if (isNotRiff)
            {
                throw new InvalidDataException($"'{path}' is not a RIFF/WAV file.");
            }

            reader.ReadInt32(); // chunk size, unused
            bool isNotWave = new string(reader.ReadChars(4)) != "WAVE";
            if (isNotWave)
            {
                throw new InvalidDataException($"'{path}' is not a WAVE file.");
            }

            short channels = 0, bitsPerSample = 0;
            int sampleRate = 0;
            short[] samples = [];
            bool hasFmtChunk = false;
            bool hasDataChunk = false;

            while (stream.Position < stream.Length)
            {
                string chunkId = new(reader.ReadChars(4));
                int chunkSize = reader.ReadInt32();
                if (chunkSize < 0)
                {
                    throw new InvalidDataException($"'{path}' has a malformed '{chunkId}' chunk.");
                }

                bool isFmtChunk = chunkId == "fmt ";
                bool isDataChunk = chunkId == "data";
                if (isFmtChunk)
                {
                    if (chunkSize < 16)
                    {
                        throw new InvalidDataException($"'{path}' has a malformed 'fmt ' chunk.");
                    }

                    short audioFormat = reader.ReadInt16();
                    channels = reader.ReadInt16();
                    sampleRate = reader.ReadInt32();
                    reader.ReadInt32(); // byte rate
                    reader.ReadInt16(); // block align
                    bitsPerSample = reader.ReadInt16();

                    int remaining = chunkSize - 16;
                    if (remaining > 0)
                    {
                        reader.ReadBytes(remaining);
                    }

                    if (audioFormat != PcmAudioFormat)
                    {
                        throw new NotSupportedException($"'{path}' uses audio format {audioFormat}; only uncompressed PCM is supported.");
                    }

                    if (bitsPerSample != SupportedBitsPerSample)
                    {
                        throw new NotSupportedException($"'{path}' uses {bitsPerSample}-bit samples; only 16-bit PCM is supported.");
                    }

                    hasFmtChunk = true;
                }
                else if (isDataChunk)
                {
                    byte[] bytes = reader.ReadBytes(chunkSize);
                    samples = new short[bytes.Length / 2];
                    Buffer.BlockCopy(bytes, 0, samples, 0, samples.Length * 2);
                    hasDataChunk = true;
                }
                else
                {
                    reader.ReadBytes(chunkSize);
                }

                // RIFF chunks are word-aligned: a chunk with an odd size is followed by one pad byte that its size doesn't
                // count. Without skipping it, the next chunk's id is read one byte early and the rest of the file is garbage,
                // so a file with an odd-sized chunk (e.g. a LIST/INFO tag) before its data chunk looks like it has no audio.
                // The last chunk may be the unpadded end of the file.
                bool hasPadByte = chunkSize % 2 == 1 && stream.Position < stream.Length;
                if (hasPadByte)
                {
                    reader.ReadByte();
                }
            }

            bool hasNoValidFormat = !hasFmtChunk || channels <= 0 || sampleRate <= 0;
            if (hasNoValidFormat)
            {
                throw new InvalidDataException($"'{path}' has no valid 'fmt ' chunk.");
            }

            bool hasNoDataChunk = !hasDataChunk;
            if (hasNoDataChunk)
            {
                throw new InvalidDataException($"'{path}' has no 'data' chunk.");
            }

            return new WavFile { Channels = channels, SampleRate = sampleRate, BitsPerSample = bitsPerSample, Samples = samples, };
        }
        catch (EndOfStreamException ex)
        {
            // A chunk claimed more bytes than the file actually has left - report it as the corrupt/truncated file it
            // is, rather than letting a raw EndOfStreamException (with no file path) surface to callers.
            throw new InvalidDataException($"'{path}' is truncated or corrupt.", ex);
        }
    }
    #endregion

    #region Public methods
    ///<summary>
    ///Reads a WAV file from <paramref name="path"/> and returns its parsed format and samples.
    ///</summary>
    ///<exception cref="ArgumentException"><paramref name="path"/> is null, empty, or whitespace.</exception>
    ///<exception cref="FileNotFoundException"><paramref name="path"/> does not exist.</exception>
    ///<exception cref="InvalidDataException">The file isn't a well-formed RIFF/WAV file, or is truncated/corrupt.</exception>
    ///<exception cref="NotSupportedException">The file isn't uncompressed 16-bit PCM.</exception>
    public static async Task<WavFile> ReadAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        // The parsing is synchronous, so it runs on a worker thread: a long clip would otherwise freeze the screen of
        // whichever page asked for it.
        return await Task.Run(() => Read(path)).ConfigureAwait(false);
    }

    ///<summary>
    ///Writes this instance to <paramref name="path"/> as a 16-bit PCM WAV file.
    ///</summary>
    ///<exception cref="ArgumentException"><paramref name="path"/> is null, empty, or whitespace.</exception>
    ///<exception cref="InvalidOperationException">
    ///<see cref="Channels"/> or <see cref="SampleRate"/> is not positive, or <see cref="BitsPerSample"/> isn't 16.
    ///</exception>
    public async Task WriteAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        bool hasInvalidFormat = Channels <= 0 || SampleRate <= 0 || BitsPerSample != SupportedBitsPerSample;
        if (hasInvalidFormat)
        {
            throw new InvalidOperationException($"Cannot write a WAV file with {Channels} channel(s), a {SampleRate} Hz sample rate, and {BitsPerSample}-bit samples.");
        }

        await using FileStream stream = File.Create(path);
        await using BinaryWriter writer = new(stream);

        int dataSize = Samples.Length * 2;
        int byteRate = SampleRate * Channels * (BitsPerSample / 8);
        short blockAlign = (short)(Channels * (BitsPerSample / 8));

        writer.Write("RIFF".ToCharArray());
        writer.Write(36 + dataSize);
        writer.Write("WAVE".ToCharArray());

        writer.Write("fmt ".ToCharArray());
        writer.Write(16);
        writer.Write(PcmAudioFormat);
        writer.Write(Channels);
        writer.Write(SampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(BitsPerSample);

        writer.Write("data".ToCharArray());
        writer.Write(dataSize);

        byte[] bytes = new byte[dataSize];
        Buffer.BlockCopy(Samples, 0, bytes, 0, dataSize);
        writer.Write(bytes);
    }
    #endregion

    #region Public properties
    public short BitsPerSample { get; init; }

    public short Channels { get; init; }

    public TimeSpan Duration => Channels == 0 || SampleRate == 0 ? TimeSpan.Zero : TimeSpan.FromSeconds((double)Samples.Length / Channels / SampleRate);

    public int SampleRate { get; init; }

    ///<summary>
    ///Interleaved PCM samples, one entry per channel per frame.
    ///</summary>
    public short[] Samples { get; init; } = [];
    #endregion
}
