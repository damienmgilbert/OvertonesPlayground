using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.Services.Implementations;

/// <inheritdoc cref="IAudioEditorService" />
public class AudioEditorService : IAudioEditorService
{
    /// <summary>App-private folder where every edit's derived output file is written.</summary>
    private static string ExportsDirectory
    {
        get
        {
            var dir = Path.Combine(FileSystem.AppDataDirectory, "Exports");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <inheritdoc />
    public async Task<float[]> GetWaveformPeaksAsync(string filePath, int peakCount)
    {
        var wav = await WavFile.ReadAsync(filePath);
        if (wav.Samples.Length == 0 || peakCount <= 0)
        {
            return [];
        }

        var peaks = new float[peakCount];
        var samplesPerPeak = Math.Max(1, wav.Samples.Length / peakCount);

        for (var i = 0; i < peakCount; i++)
        {
            var start = i * samplesPerPeak;
            var end = Math.Min(start + samplesPerPeak, wav.Samples.Length);
            short max = 0;
            for (var j = start; j < end; j++)
            {
                var abs = Math.Abs((int)wav.Samples[j]);
                if (abs > max)
                {
                    max = (short)abs;
                }
            }

            peaks[i] = max / (float)short.MaxValue;
        }

        return peaks;
    }

    /// <inheritdoc />
    public async Task<string> TrimAsync(string sourcePath, TimeSpan start, TimeSpan end, string outputName)
    {
        var wav = await WavFile.ReadAsync(sourcePath);
        var frameCount = wav.Channels * wav.SampleRate;

        var startIndex = Math.Clamp((int)(start.TotalSeconds * frameCount), 0, wav.Samples.Length);
        var endIndex = Math.Clamp((int)(end.TotalSeconds * frameCount), startIndex, wav.Samples.Length);

        var trimmed = wav.Samples[startIndex..endIndex];
        return await SaveDerivedAsync(wav, trimmed, outputName);
    }

    /// <inheritdoc />
    public async Task<string> ApplyGainAsync(string sourcePath, double gainDb, string outputName)
    {
        var wav = await WavFile.ReadAsync(sourcePath);
        var factor = Math.Pow(10, gainDb / 20.0);

        var output = new short[wav.Samples.Length];
        for (var i = 0; i < wav.Samples.Length; i++)
        {
            output[i] = ClampToShort(wav.Samples[i] * factor);
        }

        return await SaveDerivedAsync(wav, output, outputName);
    }

    /// <inheritdoc />
    public async Task<string> ApplyFadeAsync(string sourcePath, TimeSpan fadeIn, TimeSpan fadeOut, string outputName)
    {
        var wav = await WavFile.ReadAsync(sourcePath);
        var frameCount = wav.Channels * wav.SampleRate;
        var fadeInFrames = (int)(fadeIn.TotalSeconds * frameCount);
        var fadeOutFrames = (int)(fadeOut.TotalSeconds * frameCount);

        var output = (short[])wav.Samples.Clone();

        for (var i = 0; i < fadeInFrames && i < output.Length; i++)
        {
            var multiplier = (double)i / fadeInFrames;
            output[i] = ClampToShort(output[i] * multiplier);
        }

        for (var i = 0; i < fadeOutFrames && i < output.Length; i++)
        {
            var index = output.Length - 1 - i;
            var multiplier = (double)i / fadeOutFrames;
            output[index] = ClampToShort(output[index] * multiplier);
        }

        return await SaveDerivedAsync(wav, output, outputName);
    }

    /// <inheritdoc />
    public async Task<string> ReverseAsync(string sourcePath, string outputName)
    {
        var wav = await WavFile.ReadAsync(sourcePath);
        var frames = wav.Samples.Length / wav.Channels;
        var output = new short[wav.Samples.Length];

        for (var frame = 0; frame < frames; frame++)
        {
            var sourceFrame = frames - 1 - frame;
            for (var ch = 0; ch < wav.Channels; ch++)
            {
                output[(frame * wav.Channels) + ch] = wav.Samples[(sourceFrame * wav.Channels) + ch];
            }
        }

        return await SaveDerivedAsync(wav, output, outputName);
    }

    /// <inheritdoc />
    public async Task<string> NormalizeAsync(string sourcePath, string outputName)
    {
        var wav = await WavFile.ReadAsync(sourcePath);
        if (wav.Samples.Length == 0)
        {
            return await SaveDerivedAsync(wav, wav.Samples, outputName);
        }

        short peak = 1;
        foreach (var sample in wav.Samples)
        {
            var abs = (short)Math.Abs((int)sample);
            if (abs > peak)
            {
                peak = abs;
            }
        }

        var factor = short.MaxValue / (double)peak;
        var output = new short[wav.Samples.Length];
        for (var i = 0; i < wav.Samples.Length; i++)
        {
            output[i] = ClampToShort(wav.Samples[i] * factor);
        }

        return await SaveDerivedAsync(wav, output, outputName);
    }

    /// <summary>Rounds and clamps a sample value into the valid 16-bit PCM range.</summary>
    private static short ClampToShort(double value) =>
        (short)Math.Clamp(value, short.MinValue, short.MaxValue);

    /// <summary>Writes <paramref name="samples"/> as a new WAV file alongside <paramref name="source"/>'s format and returns its path.</summary>
    private static async Task<string> SaveDerivedAsync(WavFile source, short[] samples, string outputName)
    {
        var derived = new WavFile
        {
            Channels = source.Channels,
            SampleRate = source.SampleRate,
            BitsPerSample = source.BitsPerSample,
            Samples = samples,
        };

        var fileName = $"{outputName}_{DateTime.Now:yyyyMMdd_HHmmss}.wav";
        var path = Path.Combine(ExportsDirectory, fileName);
        await derived.WriteAsync(path);
        return path;
    }
}
