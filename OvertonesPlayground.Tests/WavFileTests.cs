using System.Text;
using OvertonesPlayground.Services.Implementations;

namespace OvertonesPlayground.Tests;

public sealed class WavFileTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("overtones-wav-tests-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    #region Helpers
    private string PathFor(string name) => Path.Combine(_directory, name);

    private async Task<string> WriteRawAsync(byte[] bytes)
    {
        string path = PathFor($"{Guid.NewGuid():N}.wav");
        await File.WriteAllBytesAsync(path, bytes, TestContext.Current.CancellationToken);
        return path;
    }

    /// <summary>
    /// A RIFF chunk. Follows the spec: a body of odd length is followed by one pad byte that the chunk's size doesn't count,
    /// unless <paramref name="padOddBody"/> is false (as a file's final chunk may legitimately be).
    /// </summary>
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

    private static byte[] DataChunk(params short[] samples)
    {
        byte[] bytes = new byte[samples.Length * 2];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        return Chunk("data", bytes);
    }

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
    #endregion

    #region Round trip
    [Fact]
    public async Task WriteAsync_ThenReadAsync_RoundTripsFormatAndSamples()
    {
        short[] samples = [0, 1, -1, 1000, -1000, short.MaxValue, short.MinValue, 42];
        WavFile original = new() { Channels = 2, SampleRate = 44100, BitsPerSample = 16, Samples = samples };
        string path = PathFor("roundtrip.wav");

        await original.WriteAsync(path);
        WavFile loaded = await WavFile.ReadAsync(path);

        Assert.Equal(2, loaded.Channels);
        Assert.Equal(44100, loaded.SampleRate);
        Assert.Equal(16, loaded.BitsPerSample);
        Assert.Equal(samples, loaded.Samples);
    }

    [Fact]
    public async Task WriteAsync_WritesAStandardPcmHeader()
    {
        WavFile file = new() { Channels = 1, SampleRate = 8000, BitsPerSample = 16, Samples = [1, 2, 3] };
        string path = PathFor("header.wav");

        await file.WriteAsync(path);
        byte[] bytes = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal("RIFF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal(36 + 6, BitConverter.ToInt32(bytes, 4));
        Assert.Equal("WAVEfmt ", Encoding.ASCII.GetString(bytes, 8, 8));
        Assert.Equal(1, BitConverter.ToInt16(bytes, 20)); // PCM
        Assert.Equal(8000 * 2, BitConverter.ToInt32(bytes, 28)); // byte rate
        Assert.Equal("data", Encoding.ASCII.GetString(bytes, 36, 4));
        Assert.Equal(6, BitConverter.ToInt32(bytes, 40));
        Assert.Equal(44 + 6, bytes.Length);
    }
    #endregion

    #region Reading well-formed files
    [Fact]
    public async Task ReadAsync_UnknownChunkBetweenFmtAndData_IsSkipped()
    {
        byte[] bytes = Riff("WAVE", FmtChunk(), Chunk("LIST", new byte[8]), DataChunk(10, 20, 30));
        string path = await WriteRawAsync(bytes);

        WavFile file = await WavFile.ReadAsync(path);

        Assert.Equal([10, 20, 30], file.Samples);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(25)]
    public async Task ReadAsync_OddSizedChunkBeforeData_SkipsItsPadByte(int listChunkSize)
    {
        // A LIST/INFO tag of odd length is followed by a pad byte; the data chunk after it must still be found.
        byte[] bytes = Riff("WAVE", FmtChunk(), Chunk("LIST", new byte[listChunkSize]), DataChunk(10, 20, 30));
        string path = await WriteRawAsync(bytes);

        WavFile file = await WavFile.ReadAsync(path);

        Assert.Equal([10, 20, 30], file.Samples);
        Assert.Equal(8000, file.SampleRate);
    }

    [Fact]
    public async Task ReadAsync_OddSizedChunkBeforeFmt_SkipsItsPadByte()
    {
        byte[] bytes = Riff("WAVE", Chunk("bext", new byte[9]), FmtChunk(sampleRate: 22050), DataChunk(4, 5));
        string path = await WriteRawAsync(bytes);

        WavFile file = await WavFile.ReadAsync(path);

        Assert.Equal(22050, file.SampleRate);
        Assert.Equal([4, 5], file.Samples);
    }

    [Fact]
    public async Task ReadAsync_SeveralOddSizedChunksInARow_SkipsEachPadByte()
    {
        byte[] bytes = Riff("WAVE", FmtChunk(), Chunk("LIST", new byte[3]), Chunk("junk", new byte[5]), Chunk("id3 ", new byte[11]), DataChunk(1, 2));
        string path = await WriteRawAsync(bytes);

        WavFile file = await WavFile.ReadAsync(path);

        Assert.Equal([1, 2], file.Samples);
    }

    [Fact]
    public async Task ReadAsync_OddSizedFinalChunkWithoutPadByte_IsStillRead()
    {
        // The file ends right after the last chunk's body, so there is no pad byte to skip.
        byte[] bytes = Riff("WAVE", FmtChunk(), DataChunk(7, 8), Chunk("LIST", new byte[5], padOddBody: false));
        string path = await WriteRawAsync(bytes);

        WavFile file = await WavFile.ReadAsync(path);

        Assert.Equal([7, 8], file.Samples);
    }

    [Fact]
    public async Task ReadAsync_OddSizedDataChunkAtTheEnd_KeepsWholeSamplesAndDropsTheStrayByte()
    {
        byte[] oddData = Chunk("data", [1, 0, 2, 0, 9], padOddBody: false);
        string path = await WriteRawAsync(Riff("WAVE", FmtChunk(), oddData));

        WavFile file = await WavFile.ReadAsync(path);

        Assert.Equal([1, 2], file.Samples);
    }

    [Fact]
    public async Task ReadAsync_FmtChunkWithExtraBytes_ReadsTheFormatAndSkipsTheExtension()
    {
        byte[] bytes = Riff("WAVE", FmtChunk(sampleRate: 22050, extraBytes: 2), DataChunk(5, 6));
        string path = await WriteRawAsync(bytes);

        WavFile file = await WavFile.ReadAsync(path);

        Assert.Equal(22050, file.SampleRate);
        Assert.Equal([5, 6], file.Samples);
    }

    [Fact]
    public async Task ReadAsync_EmptyDataChunk_YieldsNoSamplesAndZeroDuration()
    {
        string path = await WriteRawAsync(Riff("WAVE", FmtChunk(), DataChunk()));

        WavFile file = await WavFile.ReadAsync(path);

        Assert.Empty(file.Samples);
        Assert.Equal(TimeSpan.Zero, file.Duration);
    }
    #endregion

    #region Reading rejected files
    [Fact]
    public async Task ReadAsync_NotARiffFile_ThrowsInvalidData()
    {
        string path = await WriteRawAsync(Riff("WAVE", FmtChunk(), DataChunk(1)).Select((b, i) => i < 4 ? (byte)'X' : b).ToArray());

        await Assert.ThrowsAsync<InvalidDataException>(() => WavFile.ReadAsync(path));
    }

    [Fact]
    public async Task ReadAsync_RiffButNotWave_ThrowsInvalidData()
    {
        string path = await WriteRawAsync(Riff("AVI ", FmtChunk(), DataChunk(1)));

        await Assert.ThrowsAsync<InvalidDataException>(() => WavFile.ReadAsync(path));
    }

    [Fact]
    public async Task ReadAsync_NonPcmAudioFormat_ThrowsNotSupported()
    {
        string path = await WriteRawAsync(Riff("WAVE", FmtChunk(audioFormat: 3), DataChunk(1)));

        await Assert.ThrowsAsync<NotSupportedException>(() => WavFile.ReadAsync(path));
    }

    [Fact]
    public async Task ReadAsync_EightBitSamples_ThrowsNotSupported()
    {
        string path = await WriteRawAsync(Riff("WAVE", FmtChunk(bitsPerSample: 8), DataChunk(1)));

        await Assert.ThrowsAsync<NotSupportedException>(() => WavFile.ReadAsync(path));
    }

    [Fact]
    public async Task ReadAsync_NoFmtChunk_ThrowsInvalidData()
    {
        string path = await WriteRawAsync(Riff("WAVE", DataChunk(1, 2)));

        await Assert.ThrowsAsync<InvalidDataException>(() => WavFile.ReadAsync(path));
    }

    [Fact]
    public async Task ReadAsync_NoDataChunk_ThrowsInvalidData()
    {
        string path = await WriteRawAsync(Riff("WAVE", FmtChunk()));

        await Assert.ThrowsAsync<InvalidDataException>(() => WavFile.ReadAsync(path));
    }

    [Fact]
    public async Task ReadAsync_ZeroChannels_ThrowsInvalidData()
    {
        string path = await WriteRawAsync(Riff("WAVE", FmtChunk(channels: 0), DataChunk(1)));

        await Assert.ThrowsAsync<InvalidDataException>(() => WavFile.ReadAsync(path));
    }

    [Fact]
    public async Task ReadAsync_FmtChunkTooSmall_ThrowsInvalidData()
    {
        string path = await WriteRawAsync(Riff("WAVE", Chunk("fmt ", new byte[8]), DataChunk(1)));

        await Assert.ThrowsAsync<InvalidDataException>(() => WavFile.ReadAsync(path));
    }

    [Fact]
    public async Task ReadAsync_FileEndsInsideAChunkHeader_ThrowsInvalidDataInsteadOfEndOfStream()
    {
        // The fmt chunk claims 16 bytes but the file ends after 4.
        byte[] truncatedFmt = [.. "fmt "u8, 16, 0, 0, 0, 1, 0, 1, 0];
        string path = await WriteRawAsync(Riff("WAVE", truncatedFmt));

        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(() => WavFile.ReadAsync(path));

        Assert.IsType<EndOfStreamException>(exception.InnerException);
    }

    [Fact]
    public async Task ReadAsync_MissingFile_ThrowsFileNotFound()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() => WavFile.ReadAsync(PathFor("does-not-exist.wav")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ReadAsync_BlankPath_ThrowsArgumentException(string path)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => WavFile.ReadAsync(path));
    }
    #endregion

    #region Writing rejected files
    [Theory]
    [InlineData(0, 44100, 16)]
    [InlineData(1, 0, 16)]
    [InlineData(1, 44100, 8)]
    public async Task WriteAsync_InvalidFormat_ThrowsInvalidOperation(short channels, int sampleRate, short bitsPerSample)
    {
        WavFile file = new() { Channels = channels, SampleRate = sampleRate, BitsPerSample = bitsPerSample, Samples = [1, 2] };

        await Assert.ThrowsAsync<InvalidOperationException>(() => file.WriteAsync(PathFor("invalid.wav")));
    }

    [Fact]
    public async Task WriteAsync_BlankPath_ThrowsArgumentException()
    {
        WavFile file = new() { Channels = 1, SampleRate = 8000, BitsPerSample = 16 };

        await Assert.ThrowsAsync<ArgumentException>(() => file.WriteAsync(" "));
    }
    #endregion

    #region Duration
    [Theory]
    [InlineData(1, 8000, 8000, 1.0)]
    [InlineData(2, 8000, 16000, 1.0)]
    [InlineData(1, 44100, 22050, 0.5)]
    public void Duration_ReflectsFramesNotRawSamples(short channels, int sampleRate, int sampleCount, double expectedSeconds)
    {
        WavFile file = new() { Channels = channels, SampleRate = sampleRate, BitsPerSample = 16, Samples = new short[sampleCount] };

        Assert.Equal(expectedSeconds, file.Duration.TotalSeconds, 6);
    }

    [Fact]
    public void Duration_NoFormatSet_IsZeroRatherThanDividingByZero()
    {
        WavFile file = new() { Samples = new short[100] };

        Assert.Equal(TimeSpan.Zero, file.Duration);
    }
    #endregion
}
