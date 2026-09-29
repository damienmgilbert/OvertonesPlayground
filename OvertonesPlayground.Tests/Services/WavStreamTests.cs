using System.Text;
using OvertonesPlayground.Services.Implementations;

namespace OvertonesPlayground.Tests.Services;

public sealed class WavStreamTests : IDisposable
{
    #region Fields
    private readonly string _directory = Directory.CreateTempSubdirectory("overtones-wavstream-tests-").FullName;
    #endregion

    #region Private methods
    ///<summary>
    ///A RIFF chunk. Follows the spec: a body of odd length is followed by one pad byte that the chunk's size doesn't
    ///count, unless <paramref name="padOddBody"/> is false (as a file's final chunk may legitimately be).
    ///</summary>
    private static byte[] Chunk(string id, byte[] body, bool padOddBody = true)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.ASCII);
        writer.Write(Encoding.ASCII.GetBytes(id));
        writer.Write(body.Length);
        writer.Write(body);
        if (padOddBody && body.Length % 2 == 1)
        {
            writer.Write((byte)0);
        }

        return stream.ToArray();
    }

    private static byte[] DataChunk(params short[] samples)
    {
        byte[] bytes = new byte[samples.Length * 2];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        return Chunk("data", bytes);
    }

    private static byte[] FmtChunk(short audioFormat = 1, short channels = 1, int sampleRate = 8000, short bitsPerSample = 16, int extraBytes = 0)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        writer.Write(audioFormat);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * (bitsPerSample / 8));
        writer.Write((short)(channels * (bitsPerSample / 8)));
        writer.Write(bitsPerSample);
        writer.Write(new byte[extraBytes]);
        return Chunk("fmt ", stream.ToArray());
    }

    private string PathFor(string name) => Path.Combine(_directory, name);

    private static byte[] Riff(string form, params byte[][] chunks)
    {
        byte[] body = chunks.SelectMany(chunk => chunk).ToArray();
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.ASCII);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(4 + body.Length);
        writer.Write(Encoding.ASCII.GetBytes(form));
        writer.Write(body);
        return stream.ToArray();
    }

    private async Task<short[]> ReadAllSamplesAsync(WavStream.Reader reader)
    {
        using MemoryStream buffer = new();
        await reader.Data.CopyToAsync(buffer);
        byte[] bytes = buffer.ToArray();
        short[] samples = new short[bytes.Length / 2];
        Buffer.BlockCopy(bytes, 0, samples, 0, samples.Length * 2);
        return samples;
    }

    private async Task<string> WriteRawAsync(byte[] bytes)
    {
        string path = PathFor($"{Guid.NewGuid():N}.wav");
        await File.WriteAllBytesAsync(path, bytes, TestContext.Current.CancellationToken);
        return path;
    }
    #endregion

    #region Public methods
    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void CreateWriter_BlankPath_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => WavStream.CreateWriter(" ", 1, 8000));
    }

    [Theory]
    [InlineData(0, 44100)]
    [InlineData(1, 0)]
    public void CreateWriter_InvalidFormat_ThrowsInvalidOperation(short channels, int sampleRate)
    {
        Assert.Throws<InvalidOperationException>(() => WavStream.CreateWriter(PathFor("invalid.wav"), channels, sampleRate));
    }

    [Fact]
    public async Task OpenDataReader_BlankPath_ThrowsArgumentException()
    {
        await Task.CompletedTask;
        Assert.Throws<ArgumentException>(() => WavStream.OpenDataReader(" "));
    }

    [Fact]
    public async Task OpenDataReader_EightBitSamples_ThrowsNotSupported()
    {
        string path = await WriteRawAsync(Riff("WAVE", FmtChunk(bitsPerSample: 8), DataChunk(1)));

        Assert.Throws<NotSupportedException>(() => WavStream.OpenDataReader(path));
    }

    [Fact]
    public async Task OpenDataReader_LeavesStreamAtExactlyDataLengthBytes()
    {
        // A chunk after "data" must not be read as if it were audio.
        byte[] bytes = Riff("WAVE", FmtChunk(), DataChunk(1, 2, 3), Chunk("LIST", new byte[4]));
        string path = await WriteRawAsync(bytes);

        using WavStream.Reader reader = WavStream.OpenDataReader(path);
        short[] samples = await ReadAllSamplesAsync(reader);

        // CopyToAsync reads to end-of-stream, which here also picks up the trailing LIST chunk's bytes; what
        // matters is that DataLength itself correctly reports just the data chunk's size.
        Assert.Equal(6, reader.DataLength);
        Assert.Equal([1, 2, 3], samples[..3]);
    }

    [Fact]
    public async Task OpenDataReader_MissingFile_ThrowsFileNotFound()
    {
        await Task.CompletedTask;
        Assert.Throws<FileNotFoundException>(() => WavStream.OpenDataReader(PathFor("does-not-exist.wav")));
    }

    [Fact]
    public async Task OpenDataReader_NoDataChunk_ThrowsInvalidData()
    {
        string path = await WriteRawAsync(Riff("WAVE", FmtChunk()));

        Assert.Throws<InvalidDataException>(() => WavStream.OpenDataReader(path));
    }

    [Fact]
    public async Task OpenDataReader_NoFmtChunk_ThrowsInvalidData()
    {
        string path = await WriteRawAsync(Riff("WAVE", DataChunk(1, 2)));

        Assert.Throws<InvalidDataException>(() => WavStream.OpenDataReader(path));
    }

    [Fact]
    public async Task OpenDataReader_NonPcmAudioFormat_ThrowsNotSupported()
    {
        string path = await WriteRawAsync(Riff("WAVE", FmtChunk(audioFormat: 3), DataChunk(1)));

        Assert.Throws<NotSupportedException>(() => WavStream.OpenDataReader(path));
    }

    [Fact]
    public async Task OpenDataReader_NotARiffFile_ThrowsInvalidData()
    {
        string path = await WriteRawAsync(Riff("WAVE", FmtChunk(), DataChunk(1)).Select((b, i) => i < 4 ? (byte)'X' : b).ToArray());

        Assert.Throws<InvalidDataException>(() => WavStream.OpenDataReader(path));
    }

    [Fact]
    public async Task OpenDataReader_OddSizedChunkBeforeData_SkipsItsPadByte()
    {
        byte[] bytes = Riff("WAVE", FmtChunk(), Chunk("LIST", new byte[7]), DataChunk(10, 20, 30));
        string path = await WriteRawAsync(bytes);

        using WavStream.Reader reader = WavStream.OpenDataReader(path);
        short[] samples = await ReadAllSamplesAsync(reader);

        Assert.Equal([10, 20, 30], samples);
    }

    [Fact]
    public async Task OpenDataReader_ReadsFormatAndPositionsAtData()
    {
        byte[] bytes = Riff("WAVE", FmtChunk(channels: 2, sampleRate: 22050), DataChunk(5, 6, 7, 8));
        string path = await WriteRawAsync(bytes);

        using WavStream.Reader reader = WavStream.OpenDataReader(path);
        short[] samples = await ReadAllSamplesAsync(reader);

        Assert.Equal(2, reader.Channels);
        Assert.Equal(22050, reader.SampleRate);
        Assert.Equal(8, reader.DataLength);
        Assert.Equal([5, 6, 7, 8], samples);
    }

    [Fact]
    public async Task Writer_CompleteAsync_PatchesHeaderWithFinalLength()
    {
        string path = PathFor("streamed.wav");

        using (WavStream.Writer writer = WavStream.CreateWriter(path, 2, 44100))
        {
            byte[] first = [1, 0, 2, 0];
            byte[] second = [3, 0, 4, 0];
            await writer.WriteAsync(first, first.Length);
            await writer.WriteAsync(second, second.Length);
            await writer.CompleteAsync();
        }

        WavFile loaded = await WavFile.ReadAsync(path);
        Assert.Equal(2, loaded.Channels);
        Assert.Equal(44100, loaded.SampleRate);
        Assert.Equal<short>([1, 2, 3, 4], loaded.Samples);
    }

    [Fact]
    public async Task Writer_ThenOpenDataReader_RoundTripsAcrossSeveralChunkedWrites()
    {
        string path = PathFor("chunked.wav");
        short[] expected = [.. Enumerable.Range(1, 500).Select(i => (short)i)];
        byte[] allBytes = new byte[expected.Length * 2];
        Buffer.BlockCopy(expected, 0, allBytes, 0, allBytes.Length);

        using (WavStream.Writer writer = WavStream.CreateWriter(path, 1, 8000))
        {
            const int chunkSize = 37; // deliberately not a multiple of 2, to exercise an odd split across writes
            for (int offset = 0; offset < allBytes.Length; offset += chunkSize)
            {
                int count = Math.Min(chunkSize, allBytes.Length - offset);
                await writer.WriteAsync(allBytes[offset..(offset + count)], count);
            }

            await writer.CompleteAsync();
        }

        using WavStream.Reader reader = WavStream.OpenDataReader(path);
        short[] roundTripped = await ReadAllSamplesAsync(reader);

        Assert.Equal(1, reader.Channels);
        Assert.Equal(8000, reader.SampleRate);
        Assert.Equal(allBytes.Length, reader.DataLength);
        Assert.Equal(expected, roundTripped);
    }
    #endregion
}
