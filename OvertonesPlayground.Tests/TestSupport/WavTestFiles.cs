using OvertonesPlayground.Services.Implementations;

namespace OvertonesPlayground.Tests.TestSupport;

/// <summary>
/// Writes and reads small WAV files for the service tests.
/// </summary>
internal static class WavTestFiles
{
    /// <summary>
    /// Writes <paramref name="samples"/> (interleaved when there is more than one channel) as a 16-bit WAV file. The default rate is
    /// 1 kHz so that "one second" is a thousand frames and a test can reason about sample indexes by hand.
    /// </summary>
    public static async Task<string> WriteAsync(string path, short[] samples, int channels = 1, int sampleRate = 1000)
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        WavFile wav = new() { Channels = (short)channels, SampleRate = sampleRate, BitsPerSample = 16, Samples = samples };
        await wav.WriteAsync(path);
        return path;
    }

    public static Task<WavFile> ReadAsync(string path) => WavFile.ReadAsync(path);

    /// <summary>
    /// 1, 2, 3, ... <paramref name="count"/>: every sample is its own position, so where a slice came from can be read off its values.
    /// </summary>
    public static short[] Ramp(int count) => [.. Enumerable.Range(1, count).Select(i => (short)i)];

    public static short[] Constant(short value, int count) => [.. Enumerable.Repeat(value, count)];
}
