namespace OvertonesPlayground.Services.Implementations;

/// <summary>
/// Minimal reader/writer for uncompressed 16-bit PCM WAV files - just enough to power the
/// trim/gain/fade/reverse/normalize tools in <see cref="AudioEditorService"/> without pulling
/// in a full audio codec library.
/// </summary>
internal sealed class WavFile
{
    public int SampleRate { get; init; }

    public short Channels { get; init; }

    public short BitsPerSample { get; init; }

    /// <summary>Interleaved PCM samples, one entry per channel per frame.</summary>
    public short[] Samples { get; init; } = [];

    public TimeSpan Duration =>
        Channels == 0 || SampleRate == 0
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds((double)Samples.Length / Channels / SampleRate);

    public static async Task<WavFile> ReadAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);

        if (new string(reader.ReadChars(4)) != "RIFF")
        {
            throw new InvalidDataException($"'{path}' is not a RIFF/WAV file.");
        }

        reader.ReadInt32(); // chunk size, unused
        if (new string(reader.ReadChars(4)) != "WAVE")
        {
            throw new InvalidDataException($"'{path}' is not a WAVE file.");
        }

        short channels = 0, bitsPerSample = 0;
        int sampleRate = 0;
        short[] samples = [];

        while (stream.Position < stream.Length)
        {
            var chunkId = new string(reader.ReadChars(4));
            var chunkSize = reader.ReadInt32();

            if (chunkId == "fmt ")
            {
                reader.ReadInt16(); // audio format (1 = PCM)
                channels = reader.ReadInt16();
                sampleRate = reader.ReadInt32();
                reader.ReadInt32(); // byte rate
                reader.ReadInt16(); // block align
                bitsPerSample = reader.ReadInt16();

                var remaining = chunkSize - 16;
                if (remaining > 0)
                {
                    reader.ReadBytes(remaining);
                }
            }
            else if (chunkId == "data")
            {
                var bytes = reader.ReadBytes(chunkSize);
                samples = new short[bytes.Length / 2];
                Buffer.BlockCopy(bytes, 0, samples, 0, samples.Length * 2);
            }
            else
            {
                reader.ReadBytes(chunkSize);
            }
        }

        return new WavFile
        {
            Channels = channels,
            SampleRate = sampleRate,
            BitsPerSample = bitsPerSample,
            Samples = samples,
        };
    }

    public async Task WriteAsync(string path)
    {
        await using var stream = File.Create(path);
        await using var writer = new BinaryWriter(stream);

        var dataSize = Samples.Length * 2;
        var byteRate = SampleRate * Channels * (BitsPerSample / 8);
        var blockAlign = (short)(Channels * (BitsPerSample / 8));

        writer.Write("RIFF".ToCharArray());
        writer.Write(36 + dataSize);
        writer.Write("WAVE".ToCharArray());

        writer.Write("fmt ".ToCharArray());
        writer.Write(16);
        writer.Write((short)1); // PCM
        writer.Write(Channels);
        writer.Write(SampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(BitsPerSample);

        writer.Write("data".ToCharArray());
        writer.Write(dataSize);

        var bytes = new byte[dataSize];
        Buffer.BlockCopy(Samples, 0, bytes, 0, dataSize);
        writer.Write(bytes);
    }
}
