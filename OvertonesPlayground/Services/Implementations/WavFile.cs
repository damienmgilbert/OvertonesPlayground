using CommunityToolkit.Diagnostics;

namespace OvertonesPlayground.Services.Implementations;

///<summary>
///Minimal reader/writer for uncompressed 16-bit PCM WAV files - just enough to power the
///trim/gain/fade/reverse/normalize tools in <see cref="AudioEditorService"/> without pulling in a full audio codec
///library.
///</summary>
internal sealed class WavFile
{
    #region Public methods
    public static async Task<WavFile> ReadAsync(string path)
    {
        await using FileStream stream = File.OpenRead(path);
        using BinaryReader reader = new(stream);

        bool isNotRiff = new string(reader.ReadChars(4)) != "RIFF";
        if (isNotRiff)
        {
            ThrowHelper.ThrowInvalidDataException($"'{path}' is not a RIFF/WAV file.");
        }

        reader.ReadInt32(); // chunk size, unused
        bool isNotWave = new string(reader.ReadChars(4)) != "WAVE";
        if (isNotWave)
        {
            ThrowHelper.ThrowInvalidDataException($"'{path}' is not a WAVE file.");
        }

        short channels = 0, bitsPerSample = 0;
        int sampleRate = 0;
        short[] samples = [];

        while (stream.Position < stream.Length)
        {
            string chunkId = new(reader.ReadChars(4));
            int chunkSize = reader.ReadInt32();

            bool isFmtChunk = chunkId == "fmt ";
            bool isDataChunk = chunkId == "data";
            if (isFmtChunk)
            {
                reader.ReadInt16(); // audio format (1 = PCM)
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
            }
            else if (isDataChunk)
            {
                byte[] bytes = reader.ReadBytes(chunkSize);
                samples = new short[bytes.Length / 2];
                Buffer.BlockCopy(bytes, 0, samples, 0, samples.Length * 2);
            }
            else
            {
                reader.ReadBytes(chunkSize);
            }
        }

        WavFile wavFile = new() { Channels = channels, SampleRate = sampleRate, BitsPerSample = bitsPerSample, Samples = samples, };
        return wavFile;
    }

    public async Task WriteAsync(string path)
    {
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
        writer.Write((short)1); // PCM
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
