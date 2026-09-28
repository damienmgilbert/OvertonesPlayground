using System.Text;

namespace OvertonesPlayground.Services.Implementations;

///<summary>
///Streaming counterpart to <see cref="WavFile"/>: reads and writes 16-bit PCM WAV files directly against disk instead
///of buffering the whole file in memory. Used by <c>AudioFormatConverterService</c> so a multi-hour source (which
///would be hundreds of megabytes of raw PCM) never has to fit in RAM as a single array; everything that needs
///indexed sample access (trim/gain/fade/reverse/normalize) still uses <see cref="WavFile"/>.
///</summary>
internal static class WavStream
{
    #region Constants
    private const int HeaderLength = 44;
    private const short PcmAudioFormat = 1;
    private const short SupportedBitsPerSample = 16;
    #endregion

    #region Public methods
    ///<summary>
    ///Creates <paramref name="path"/> and returns a <see cref="Writer"/> ready to stream PCM bytes into it.
    ///</summary>
    ///<exception cref="ArgumentException"><paramref name="path"/> is null, empty, or whitespace.</exception>
    ///<exception cref="InvalidOperationException"><paramref name="channels"/> or <paramref name="sampleRate"/> is not positive.</exception>
    public static Writer CreateWriter(string path, short channels, int sampleRate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        bool hasInvalidFormat = channels <= 0 || sampleRate <= 0;
        if (hasInvalidFormat)
        {
            throw new InvalidOperationException($"Cannot write a WAV file with {channels} channel(s) and a {sampleRate} Hz sample rate.");
        }

        return new Writer(File.Create(path), channels, sampleRate);
    }

    ///<summary>
    ///Opens <paramref name="path"/>, parses its RIFF/fmt header without reading sample data, and returns a
    ///<see cref="Reader"/> whose <see cref="Reader.Data"/> stream is seeked to the start of the <c>data</c> chunk.
    ///</summary>
    ///<exception cref="ArgumentException"><paramref name="path"/> is null, empty, or whitespace.</exception>
    ///<exception cref="FileNotFoundException"><paramref name="path"/> does not exist.</exception>
    ///<exception cref="InvalidDataException">The file isn't a well-formed RIFF/WAV file.</exception>
    ///<exception cref="NotSupportedException">The file isn't uncompressed 16-bit PCM.</exception>
    public static Reader OpenDataReader(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        FileStream stream = File.OpenRead(path);
        try
        {
            using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: true);

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
            long dataLength = -1;
            bool hasFmtChunk = false;

            while (dataLength < 0 && stream.Position < stream.Length)
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
                    SkipPadByte(reader, stream, chunkSize);
                }
                else if (isDataChunk)
                {
                    // Leave the stream positioned right here - the caller reads the PCM bytes itself, exactly
                    // dataLength of them, so there's no need to skip a trailing pad byte or look further.
                    dataLength = chunkSize;
                }
                else
                {
                    reader.ReadBytes(chunkSize);
                    SkipPadByte(reader, stream, chunkSize);
                }
            }

            bool hasNoValidFormat = !hasFmtChunk || channels <= 0 || sampleRate <= 0;
            if (hasNoValidFormat)
            {
                throw new InvalidDataException($"'{path}' has no valid 'fmt ' chunk.");
            }

            bool hasNoDataChunk = dataLength < 0;
            if (hasNoDataChunk)
            {
                throw new InvalidDataException($"'{path}' has no 'data' chunk.");
            }

            return new Reader(stream, channels, sampleRate, dataLength);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    ///<summary>
    ///RIFF chunks are word-aligned: a chunk with an odd size is followed by one pad byte that its size doesn't count.
    ///Mirrors <see cref="WavFile"/>'s own handling of this so the same odd-sized-chunk files it accepts are found
    ///correctly here too.
    ///</summary>
    private static void SkipPadByte(BinaryReader reader, Stream stream, int chunkSize)
    {
        bool hasPadByte = chunkSize % 2 == 1 && stream.Position < stream.Length;
        if (hasPadByte)
        {
            reader.ReadByte();
        }
    }
    #endregion

    #region Nested types
    ///<summary>
    ///An opened WAV file's format, and a stream positioned at the start of its PCM data with <see cref="DataLength"/>
    ///bytes available. The caller reads exactly that many bytes; nothing past them belongs to the audio.
    ///</summary>
    public sealed class Reader : IDisposable
    {
        internal Reader(FileStream stream, short channels, int sampleRate, long dataLength)
        {
            Data = stream;
            Channels = channels;
            SampleRate = sampleRate;
            DataLength = dataLength;
        }

        public short Channels { get; }

        public Stream Data { get; }

        public long DataLength { get; }

        public int SampleRate { get; }

        public void Dispose() => Data.Dispose();
    }

    ///<summary>
    ///Streams PCM bytes straight to disk as they arrive, rather than requiring the total length up front: a 44-byte
    ///placeholder header is written immediately, and <see cref="CompleteAsync"/> patches its size fields once the
    ///final length is known.
    ///</summary>
    public sealed class Writer : IDisposable
    {
        #region Fields
        private readonly short _channels;
        private readonly int _sampleRate;
        private readonly FileStream _stream;
        private long _dataLength;
        #endregion

        #region Constructors
        internal Writer(FileStream stream, short channels, int sampleRate)
        {
            _stream = stream;
            _channels = channels;
            _sampleRate = sampleRate;
            _stream.Write(new byte[HeaderLength]);
        }
        #endregion

        #region Public methods
        ///<summary>
        ///Patches the header with the final data length now that it's known. Must be called once writing is done for
        ///the file to be a valid WAV - if an exception unwinds before this runs, the file is left as the invalid
        ///partial WAV it actually is, rather than being silently claimed complete.
        ///</summary>
        public async Task CompleteAsync()
        {
            short blockAlign = (short)(_channels * (SupportedBitsPerSample / 8));
            int byteRate = _sampleRate * blockAlign;
            int dataSize = (int)_dataLength;

            using MemoryStream headerStream = new(HeaderLength);
            await using (BinaryWriter writer = new(headerStream))
            {
                writer.Write("RIFF".ToCharArray());
                writer.Write(36 + dataSize);
                writer.Write("WAVE".ToCharArray());

                writer.Write("fmt ".ToCharArray());
                writer.Write(16);
                writer.Write(PcmAudioFormat);
                writer.Write(_channels);
                writer.Write(_sampleRate);
                writer.Write(byteRate);
                writer.Write(blockAlign);
                writer.Write(SupportedBitsPerSample);

                writer.Write("data".ToCharArray());
                writer.Write(dataSize);
            }

            _stream.Position = 0;
            await _stream.WriteAsync(headerStream.ToArray());
            await _stream.FlushAsync();
        }

        public void Dispose() => _stream.Dispose();

        ///<summary>
        ///Writes <paramref name="count"/> bytes from the start of <paramref name="buffer"/> to disk.
        ///</summary>
        public async Task WriteAsync(byte[] buffer, int count)
        {
            await _stream.WriteAsync(buffer.AsMemory(0, count));
            _dataLength += count;
        }
        #endregion
    }
    #endregion
}
