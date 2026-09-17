using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.Services.Implementations;

///<inheritdoc cref="IAudioEditorService"/>
public class AudioEditorService : IAudioEditorService
{
    #region Private methods

    ///<summary>
    ///Rounds and clamps a sample value into the valid 16-bit PCM range.
    ///</summary>
    private static short ClampToShort(double value) => (short)Math.Clamp(value, short.MinValue, short.MaxValue);

    ///<summary>
    ///Replaces characters that aren't valid in a file name (e.g. a clip named with a "/" or ":") with "_", so an
    ///arbitrary clip/operation name can never produce an invalid or unexpectedly-nested output path.
    ///</summary>
    private static string SanitizeFileNameSegment(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(c => invalid.Contains(c) ? '_' : c));
    }

    ///<summary>
    ///Writes <paramref name="samples"/> as a new WAV file alongside <paramref name="source"/>'s format and returns its
    ///path.
    ///</summary>
    private static async Task<string> SaveDerivedAsync(WavFile source, short[] samples, string outputName)
    {
        WavFile derived = new() { Channels = source.Channels, SampleRate = source.SampleRate, BitsPerSample = source.BitsPerSample, Samples = samples, };

        string fileName = $"{SanitizeFileNameSegment(outputName)}_{DateTime.Now:yyyyMMdd_HHmmss}.wav";
        string path = Path.Combine(ExportsDirectory, fileName);
        await derived.WriteAsync(path);
        return path;
    }
    #endregion

    #region Private properties
    ///<summary>
    ///App-private folder where every edit's derived output file is written.
    ///</summary>
    private static string ExportsDirectory
    {
        get
        {
            string dir = Path.Combine(FileSystem.AppDataDirectory, "Exports");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
    #endregion

    #region Public methods
    ///<inheritdoc/>
    public async Task<string> ApplyFadeAsync(string sourcePath, TimeSpan fadeIn, TimeSpan fadeOut, string outputName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputName);

        WavFile wav = await WavFile.ReadAsync(sourcePath);
        int frameCount = wav.Channels * wav.SampleRate;
        int fadeInFrames = (int)(fadeIn.TotalSeconds * frameCount);
        int fadeOutFrames = (int)(fadeOut.TotalSeconds * frameCount);

        short[] output = (short[])wav.Samples.Clone();

        for (int i = 0; i < fadeInFrames && i < output.Length; i++)
        {
            double multiplier = (double)i / fadeInFrames;
            output[i] = ClampToShort(output[i] * multiplier);
        }

        for (int i = 0; i < fadeOutFrames && i < output.Length; i++)
        {
            int index = output.Length - 1 - i;
            double multiplier = (double)i / fadeOutFrames;
            output[index] = ClampToShort(output[index] * multiplier);
        }

        string outputPath = await SaveDerivedAsync(wav, output, outputName);
        return outputPath;
    }

    ///<inheritdoc/>
    public async Task<string> ApplyGainAsync(string sourcePath, double gainDb, string outputName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputName);

        WavFile wav = await WavFile.ReadAsync(sourcePath);
        double factor = Math.Pow(10, gainDb / 20.0);

        short[] output = new short[wav.Samples.Length];
        for (int i = 0; i < wav.Samples.Length; i++)
        {
            output[i] = ClampToShort(wav.Samples[i] * factor);
        }

        string outputPath = await SaveDerivedAsync(wav, output, outputName);
        return outputPath;
    }

    ///<inheritdoc/>
    public async Task<string> CutAsync(string sourcePath, TimeSpan start, TimeSpan end, string outputName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputName);

        WavFile wav = await WavFile.ReadAsync(sourcePath);
        int frameCount = wav.Channels * wav.SampleRate;

        int startIndex = Math.Clamp((int)(start.TotalSeconds * frameCount), 0, wav.Samples.Length);
        int endIndex = Math.Clamp((int)(end.TotalSeconds * frameCount), startIndex, wav.Samples.Length);

        short[] output = new short[wav.Samples.Length - (endIndex - startIndex)];
        Array.Copy(wav.Samples, 0, output, 0, startIndex);
        Array.Copy(wav.Samples, endIndex, output, startIndex, wav.Samples.Length - endIndex);

        string outputPath = await SaveDerivedAsync(wav, output, outputName);
        return outputPath;
    }

    ///<inheritdoc/>
    public async Task<float[]> GetWaveformPeaksAsync(string filePath, int peakCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        WavFile wav = await WavFile.ReadAsync(filePath);
        bool isEmpty = wav.Samples.Length == 0;
        bool isInvalidPeakCount = peakCount <= 0;
        if (isEmpty || isInvalidPeakCount)
        {
            return [];
        }

        float[] peaks = new float[peakCount];
        int samplesPerPeak = Math.Max(1, wav.Samples.Length / peakCount);

        for (int i = 0; i < peakCount; i++)
        {
            int start = i * samplesPerPeak;
            int end = Math.Min(start + samplesPerPeak, wav.Samples.Length);
            short max = 0;
            for (int j = start; j < end; j++)
            {
                int abs = Math.Abs((int)wav.Samples[j]);
                if (abs > max)
                {
                    max = (short)abs;
                }
            }

            peaks[i] = max / (float)short.MaxValue;
        }

        return peaks;
    }

    ///<inheritdoc/>
    public async Task<string> NormalizeAsync(string sourcePath, string outputName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputName);

        WavFile wav = await WavFile.ReadAsync(sourcePath);
        bool isEmpty = wav.Samples.Length == 0;
        if (isEmpty)
        {
            return await SaveDerivedAsync(wav, wav.Samples, outputName);
        }

        short peak = 1;
        foreach (short sample in wav.Samples)
        {
            short abs = (short)Math.Abs((int)sample);
            if (abs > peak)
            {
                peak = abs;
            }
        }

        double factor = short.MaxValue / (double)peak;
        short[] output = new short[wav.Samples.Length];
        for (int i = 0; i < wav.Samples.Length; i++)
        {
            output[i] = ClampToShort(wav.Samples[i] * factor);
        }

        string outputPath = await SaveDerivedAsync(wav, output, outputName);
        return outputPath;
    }

    ///<inheritdoc/>
    public async Task<string> ReverseAsync(string sourcePath, string outputName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputName);

        WavFile wav = await WavFile.ReadAsync(sourcePath);
        int frames = wav.Samples.Length / wav.Channels;
        short[] output = new short[wav.Samples.Length];

        for (int frame = 0; frame < frames; frame++)
        {
            int sourceFrame = frames - 1 - frame;
            for (int ch = 0; ch < wav.Channels; ch++)
            {
                output[(frame * wav.Channels) + ch] = wav.Samples[(sourceFrame * wav.Channels) + ch];
            }
        }

        return await SaveDerivedAsync(wav, output, outputName);
    }

    ///<inheritdoc/>
    public async Task<string> TrimAsync(string sourcePath, TimeSpan start, TimeSpan end, string outputName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputName);

        WavFile wav = await WavFile.ReadAsync(sourcePath);
        int frameCount = wav.Channels * wav.SampleRate;

        int startIndex = Math.Clamp((int)(start.TotalSeconds * frameCount), 0, wav.Samples.Length);
        int endIndex = Math.Clamp((int)(end.TotalSeconds * frameCount), startIndex, wav.Samples.Length);

        short[] trimmed = wav.Samples[startIndex..endIndex];
        string outputPath = await SaveDerivedAsync(wav, trimmed, outputName);
        return outputPath;
    }
    #endregion
}
