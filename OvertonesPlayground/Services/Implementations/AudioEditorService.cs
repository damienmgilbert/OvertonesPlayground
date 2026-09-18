using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.Services.Implementations;

///<inheritdoc cref="IAudioEditorService"/>
public class AudioEditorService : IAudioEditorService
{
    #region Constants
    ///<summary>
    ///Q (bandwidth) shared by all three equalizer bands - a moderate, musically neutral width.
    ///</summary>
    private const double EqBandQ = 0.9;

    ///<summary>
    ///Center frequency of the equalizer's high shelf band.
    ///</summary>
    private const double EqHighShelfFrequencyHz = 6000;

    ///<summary>
    ///Center frequency of the equalizer's low shelf band.
    ///</summary>
    private const double EqLowShelfFrequencyHz = 150;

    ///<summary>
    ///Center frequency of the equalizer's mid peaking band.
    ///</summary>
    private const double EqMidPeakFrequencyHz = 1000;
    #endregion

    #region Private methods
    ///<summary>
    ///Writes <paramref name="samples"/> as a new WAV file alongside <paramref name="source"/>'s format and returns its
    ///path.
    ///</summary>
    private static Task<string> SaveDerivedAsync(WavFile source, short[] samples, string outputName)
    {
        WavFile derived = new() { Channels = source.Channels, SampleRate = source.SampleRate, BitsPerSample = source.BitsPerSample, Samples = samples, };
        return DerivedAudioFileWriter.SaveAsync(derived, ExportsDirectory, outputName);
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
    public async Task<string> ApplyCompressionAsync(string sourcePath, double thresholdDb, double ratio, double attackMs, double releaseMs, string outputName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputName);

        WavFile wav = await WavFile.ReadAsync(sourcePath);
        short[] output = (short[])wav.Samples.Clone();
        DynamicsProcessor.Compress(output, wav.Channels, wav.SampleRate, thresholdDb, Math.Max(1, ratio), attackMs, releaseMs);

        return await SaveDerivedAsync(wav, output, outputName);
    }

    ///<inheritdoc/>
    public async Task<string> ApplyEqualizerAsync(string sourcePath, double lowGainDb, double midGainDb, double highGainDb, string outputName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputName);

        WavFile wav = await WavFile.ReadAsync(sourcePath);
        short[] output = (short[])wav.Samples.Clone();

        BiquadFilter.ApplyLowShelf(output, wav.Channels, wav.SampleRate, EqLowShelfFrequencyHz, lowGainDb, EqBandQ);
        BiquadFilter.ApplyPeaking(output, wav.Channels, wav.SampleRate, EqMidPeakFrequencyHz, midGainDb, EqBandQ);
        BiquadFilter.ApplyHighShelf(output, wav.Channels, wav.SampleRate, EqHighShelfFrequencyHz, highGainDb, EqBandQ);

        return await SaveDerivedAsync(wav, output, outputName);
    }

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
            output[i] = PcmMath.ClampToShort(output[i] * multiplier);
        }

        for (int i = 0; i < fadeOutFrames && i < output.Length; i++)
        {
            int index = output.Length - 1 - i;
            double multiplier = (double)i / fadeOutFrames;
            output[index] = PcmMath.ClampToShort(output[index] * multiplier);
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
            output[i] = PcmMath.ClampToShort(wav.Samples[i] * factor);
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
    public async Task<TimeSpan> FindNearestZeroCrossingAsync(string sourcePath, TimeSpan near, TimeSpan maxSearch)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        WavFile wav = await WavFile.ReadAsync(sourcePath);
        bool hasNoSamples = wav.Samples.Length < 2;
        if (hasNoSamples)
        {
            return near;
        }

        int frameCount = wav.Channels * wav.SampleRate;
        int centerIndex = Math.Clamp((int)(near.TotalSeconds * frameCount), 0, wav.Samples.Length - 1);
        int maxOffset = Math.Max(1, (int)(maxSearch.TotalSeconds * frameCount));

        int searchStart = Math.Max(0, centerIndex - maxOffset);
        int searchEnd = Math.Min(wav.Samples.Length - 2, centerIndex + maxOffset);

        int nearestIndex = centerIndex;
        int nearestDistance = int.MaxValue;
        for (int i = searchStart; i <= searchEnd; i++)
        {
            bool isCrossing = Math.Sign(wav.Samples[i]) != Math.Sign(wav.Samples[i + 1]);
            if (!isCrossing)
            {
                continue;
            }

            int distance = Math.Abs(i - centerIndex);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = i;
            }
        }

        return TimeSpan.FromSeconds((double)nearestIndex / frameCount);
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
    public async Task<string> InsertAsync(string sourcePath, string insertPath, TimeSpan at, string outputName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(insertPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputName);

        WavFile source = await WavFile.ReadAsync(sourcePath);
        WavFile insert = await WavFile.ReadAsync(insertPath);
        short[] insertSamples = AudioFormatUtility.Conform(insert, source.Channels, source.SampleRate);

        int frameCount = source.Channels * source.SampleRate;
        int insertIndex = Math.Clamp((int)(at.TotalSeconds * frameCount), 0, source.Samples.Length);

        short[] output = new short[source.Samples.Length + insertSamples.Length];
        Array.Copy(source.Samples, 0, output, 0, insertIndex);
        Array.Copy(insertSamples, 0, output, insertIndex, insertSamples.Length);
        Array.Copy(source.Samples, insertIndex, output, insertIndex + insertSamples.Length, source.Samples.Length - insertIndex);

        string outputPath = await SaveDerivedAsync(source, output, outputName);
        return outputPath;
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
            output[i] = PcmMath.ClampToShort(wav.Samples[i] * factor);
        }

        string outputPath = await SaveDerivedAsync(wav, output, outputName);
        return outputPath;
    }

    ///<inheritdoc/>
    public async Task<string> ReduceNoiseAsync(string sourcePath, TimeSpan noiseSampleDuration, string outputName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputName);

        WavFile wav = await WavFile.ReadAsync(sourcePath);
        int noiseFrameCount = (int)(noiseSampleDuration.TotalSeconds * wav.SampleRate);

        // Spectral subtraction is CPU-heavy (many FFTs over the whole clip) compared to this service's other
        // sample-at-a-time edits, so it runs off the calling thread to avoid a UI stall on longer clips.
        short[] output = await Task.Run(() => SpectralNoiseReducer.Reduce(wav.Samples, wav.Channels, noiseFrameCount));

        return await SaveDerivedAsync(wav, output, outputName);
    }

    ///<inheritdoc/>
    public async Task<string> RemoveVocalsAsync(string sourcePath, string outputName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputName);

        WavFile wav = await WavFile.ReadAsync(sourcePath);
        bool isNotStereo = wav.Channels != 2;
        if (isNotStereo)
        {
            throw new NotSupportedException($"'{sourcePath}' has {wav.Channels} channel(s); vocal removal needs a stereo source to cancel out audio common to both channels.");
        }

        int frames = wav.Samples.Length / 2;
        short[] output = new short[wav.Samples.Length];
        for (int frame = 0; frame < frames; frame++)
        {
            int index = frame * 2;
            short difference = PcmMath.ClampToShort(wav.Samples[index] - wav.Samples[index + 1]);
            output[index] = difference;
            output[index + 1] = difference;
        }

        return await SaveDerivedAsync(wav, output, outputName);
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
    public async Task<(string BeforePath, string AfterPath)> SplitAsync(string sourcePath, TimeSpan at, string outputNameBefore, string outputNameAfter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputNameBefore);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputNameAfter);

        WavFile wav = await WavFile.ReadAsync(sourcePath);
        int frameCount = wav.Channels * wav.SampleRate;
        int splitIndex = Math.Clamp((int)(at.TotalSeconds * frameCount), 0, wav.Samples.Length);

        short[] before = wav.Samples[..splitIndex];
        short[] after = wav.Samples[splitIndex..];

        string beforePath = await SaveDerivedAsync(wav, before, outputNameBefore);
        string afterPath = await SaveDerivedAsync(wav, after, outputNameAfter);
        return (beforePath, afterPath);
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
