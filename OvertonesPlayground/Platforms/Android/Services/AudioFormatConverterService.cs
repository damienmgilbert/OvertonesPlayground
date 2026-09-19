using System.Diagnostics;
using Android.Media;
using Java.Nio;
using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Implementations;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.Platforms.Android.Services;

/// <summary>
/// Converts between 16-bit PCM WAV and compressed audio using Android's <see cref="MediaExtractor"/>/
/// <see cref="MediaCodec"/>: decoding MP3/AAC/OGG/etc. into WAV so the app's WAV-only pipeline can read them, and
/// encoding WAV to AAC/MP3 for export.
/// </summary>
public class AudioFormatConverterService : IAudioFormatConverterService
{
    /// <summary>7-byte ADTS header length - the minimal framing MediaCodec's raw AAC output needs to become a
    /// standalone, playable .aac file without a full MP4/M4A muxer.</summary>
    private const int AdtsHeaderLength = 7;

    /// <summary>The MPEG-4 Audio Object Type for AAC-LC, per the ADTS header spec's profile field (encoded as
    /// profile-1).</summary>
    private const int AacLcProfile = 2;

    /// <summary>Constant bit rate used for both AAC and (best-effort) MP3 export - fixed rather than user-configurable,
    /// consistent with this feature's "cheap DSP" scope.</summary>
    private const int EncodeBitRateBps = 128_000;

    private const long DequeueTimeoutUs = 10_000;

    /// <summary>The fewest milliseconds between two decode progress reports.</summary>
    private const long ProgressIntervalMs = 100;

    /// <summary>The 13 sample rates ADTS's 4-bit frequency-index field can represent, in index order - not an
    /// arbitrary app limitation, but what the AAC/ADTS spec itself supports.</summary>
    private static readonly int[] AdtsSampleRates = [96000, 88200, 64000, 48000, 44100, 32000, 24000, 22050, 16000, 12000, 11025, 8000, 7350];

    /// <summary>App-private folder where every converted output file is written.</summary>
    private static string ConvertedDirectory
    {
        get
        {
            string dir = Path.Combine(FileSystem.AppDataDirectory, "Converted");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <inheritdoc />
    public Task<string> ConvertFromWavAsync(string sourcePath, AudioExportFormat format, string outputName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputName);

        return Task.Run(() => EncodeFromWavAsync(sourcePath, format, outputName));
    }

    /// <inheritdoc />
    public Task<string> ConvertToWavAsync(string sourcePath, string outputName, IProgress<double>? progress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputName);

        return Task.Run(() => DecodeToWavAsync(sourcePath, outputName, progress));
    }

    /// <inheritdoc />
    public bool NeedsConversion(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return !string.Equals(Path.GetExtension(filePath), ".wav", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Tells <paramref name="progress"/> how far <paramref name="positionUs"/> is through
    /// <paramref name="durationUs"/>, but no more often than <see cref="ProgressIntervalMs"/> so a long decode doesn't flood
    /// the UI thread with updates.</summary>
    private static void ReportDecodeProgress(IProgress<double>? progress, long positionUs, long durationUs, Stopwatch clock)
    {
        bool cannotReport = progress is null || durationUs <= 0 || clock.ElapsedMilliseconds < ProgressIntervalMs;
        if (cannotReport)
        {
            return;
        }

        clock.Restart();
        progress!.Report(Math.Clamp((double)positionUs / durationUs, 0, 1));
    }

    /// <summary>Runs the blocking extractor/decoder pump loop and writes the result as a WAV file.</summary>
    private static async Task<string> DecodeToWavAsync(string sourcePath, string outputName, IProgress<double>? progress)
    {
        MediaExtractor extractor = new();
        MediaCodec? codec = null;
        try
        {
            extractor.SetDataSource(sourcePath);

            int trackIndex = -1;
            MediaFormat? trackFormat = null;
            for (int i = 0; i < extractor.TrackCount; i++)
            {
                MediaFormat format = extractor.GetTrackFormat(i)!;
                string? mime = format.GetString(MediaFormat.KeyMime);
                bool isAudioTrack = mime?.StartsWith("audio/", StringComparison.Ordinal) == true;
                if (isAudioTrack)
                {
                    trackIndex = i;
                    trackFormat = format;
                    break;
                }
            }

            bool hasNoAudioTrack = trackIndex < 0 || trackFormat is null;
            if (hasNoAudioTrack)
            {
                throw new NotSupportedException($"'{sourcePath}' has no audio track.");
            }

            extractor.SelectTrack(trackIndex);
            string mimeType = trackFormat!.GetString(MediaFormat.KeyMime)!;

            try
            {
                codec = MediaCodec.CreateDecoderByType(mimeType);
            }
            catch (Exception ex) when (ex is not NotSupportedException)
            {
                throw new NotSupportedException($"No decoder available for '{mimeType}'.", ex);
            }

            codec.Configure(trackFormat, null, null, MediaCodecConfigFlags.None);
            codec.Start();

            int channels = trackFormat.GetInteger(MediaFormat.KeyChannelCount);
            int sampleRate = trackFormat.GetInteger(MediaFormat.KeySampleRate);
            long durationUs = trackFormat.ContainsKey(MediaFormat.KeyDuration) ? trackFormat.GetLong(MediaFormat.KeyDuration) : 0;
            Stopwatch progressClock = Stopwatch.StartNew();

            using MemoryStream pcm = new();
            using MediaCodec.BufferInfo info = new();
            bool sawInputEos = false;
            bool sawOutputEos = false;

            while (!sawOutputEos)
            {
                if (!sawInputEos)
                {
                    int inputIndex = codec.DequeueInputBuffer(DequeueTimeoutUs);
                    if (inputIndex >= 0)
                    {
                        ByteBuffer inputBuffer = codec.GetInputBuffer(inputIndex)!;
                        inputBuffer.Clear();
                        int sampleSize = extractor.ReadSampleData(inputBuffer, 0);
                        if (sampleSize < 0)
                        {
                            codec.QueueInputBuffer(inputIndex, 0, 0, 0, MediaCodecBufferFlags.EndOfStream);
                            sawInputEos = true;
                        }
                        else
                        {
                            long sampleTimeUs = extractor.SampleTime;
                            codec.QueueInputBuffer(inputIndex, 0, sampleSize, sampleTimeUs, MediaCodecBufferFlags.None);
                            extractor.Advance();
                            ReportDecodeProgress(progress, sampleTimeUs, durationUs, progressClock);
                        }
                    }
                }

                int outputIndex = codec.DequeueOutputBuffer(info, DequeueTimeoutUs);
                if (outputIndex >= 0)
                {
                    if (info.Size > 0)
                    {
                        ByteBuffer outputBuffer = codec.GetOutputBuffer(outputIndex)!;
                        byte[] chunk = new byte[info.Size];
                        outputBuffer.Position(info.Offset);
                        outputBuffer.Limit(info.Offset + info.Size);
                        outputBuffer.Get(chunk);
                        pcm.Write(chunk, 0, chunk.Length);
                    }

                    codec.ReleaseOutputBuffer(outputIndex, false);

                    bool isEos = (info.Flags & MediaCodecBufferFlags.EndOfStream) != 0;
                    if (isEos)
                    {
                        sawOutputEos = true;
                    }
                }
                else if (outputIndex == (int)MediaCodecInfoState.OutputFormatChanged)
                {
                    MediaFormat newFormat = codec.OutputFormat!;
                    channels = newFormat.GetInteger(MediaFormat.KeyChannelCount);
                    sampleRate = newFormat.GetInteger(MediaFormat.KeySampleRate);
                }
            }

            byte[] bytes = pcm.ToArray();
            short[] samples = new short[bytes.Length / 2];
            System.Buffer.BlockCopy(bytes, 0, samples, 0, samples.Length * 2);

            WavFile wav = new() { Channels = (short)channels, SampleRate = sampleRate, BitsPerSample = 16, Samples = samples, };
            return await DerivedAudioFileWriter.SaveAsync(wav, ConvertedDirectory, outputName);
        }
        finally
        {
            if (codec is not null)
            {
                try
                {
                    // Stop() throws if Configure()/Start() never completed (e.g. the failure happened before
                    // then); cleanup shouldn't mask whatever exception is already unwinding.
                    codec.Stop();
                }
                catch (Exception)
                {
                }

                codec.Release();
                codec.Dispose();
            }

            extractor.Release();
            extractor.Dispose();
        }
    }

    /// <summary>Runs the blocking PCM-in/compressed-out encoder pump loop and writes the result to a file.</summary>
    private static async Task<string> EncodeFromWavAsync(string sourcePath, AudioExportFormat format, string outputName)
    {
        WavFile source = await WavFile.ReadAsync(sourcePath);
        string mimeType = format == AudioExportFormat.Aac ? "audio/mp4a-latm" : "audio/mpeg";

        int adtsFrequencyIndex = -1;
        if (format == AudioExportFormat.Aac)
        {
            adtsFrequencyIndex = Array.IndexOf(AdtsSampleRates, source.SampleRate);
            bool isUnsupportedRate = adtsFrequencyIndex < 0;
            if (isUnsupportedRate)
            {
                throw new NotSupportedException($"{source.SampleRate} Hz isn't a standard AAC sample rate; can't build an ADTS-framed .aac file.");
            }
        }

        MediaCodec? codec = null;
        try
        {
            using MediaFormat outputFormat = MediaFormat.CreateAudioFormat(mimeType, source.SampleRate, source.Channels);
            outputFormat.SetInteger(MediaFormat.KeyBitRate, EncodeBitRateBps);
            if (format == AudioExportFormat.Aac)
            {
                outputFormat.SetInteger(MediaFormat.KeyAacProfile, (int)MediaCodecProfileType.Aacobjectlc);
            }

            try
            {
                codec = MediaCodec.CreateEncoderByType(mimeType);
                codec.Configure(outputFormat, null, null, MediaCodecConfigFlags.Encode);
            }
            catch (Exception ex) when (ex is not NotSupportedException)
            {
                throw new NotSupportedException($"No encoder available for '{mimeType}' on this device.", ex);
            }

            codec.Start();

            byte[] pcmBytes = new byte[source.Samples.Length * 2];
            System.Buffer.BlockCopy(source.Samples, 0, pcmBytes, 0, pcmBytes.Length);

            using MemoryStream encoded = new();
            using MediaCodec.BufferInfo info = new();
            int inputPosition = 0;
            bool sawInputEos = false;
            bool sawOutputEos = false;

            while (!sawOutputEos)
            {
                if (!sawInputEos)
                {
                    int inputIndex = codec.DequeueInputBuffer(DequeueTimeoutUs);
                    if (inputIndex >= 0)
                    {
                        ByteBuffer inputBuffer = codec.GetInputBuffer(inputIndex)!;
                        inputBuffer.Clear();
                        int chunkSize = Math.Min(inputBuffer.Capacity(), pcmBytes.Length - inputPosition);
                        bool isDone = chunkSize <= 0;
                        if (isDone)
                        {
                            codec.QueueInputBuffer(inputIndex, 0, 0, 0, MediaCodecBufferFlags.EndOfStream);
                            sawInputEos = true;
                        }
                        else
                        {
                            inputBuffer.Put(pcmBytes, inputPosition, chunkSize);
                            codec.QueueInputBuffer(inputIndex, 0, chunkSize, 0, MediaCodecBufferFlags.None);
                            inputPosition += chunkSize;
                        }
                    }
                }

                int outputIndex = codec.DequeueOutputBuffer(info, DequeueTimeoutUs);
                if (outputIndex >= 0)
                {
                    if (info.Size > 0)
                    {
                        ByteBuffer outputBuffer = codec.GetOutputBuffer(outputIndex)!;
                        byte[] chunk = new byte[info.Size];
                        outputBuffer.Position(info.Offset);
                        outputBuffer.Limit(info.Offset + info.Size);
                        outputBuffer.Get(chunk);

                        if (format == AudioExportFormat.Aac)
                        {
                            WriteAdtsHeader(encoded, chunk.Length, adtsFrequencyIndex, source.Channels);
                        }

                        encoded.Write(chunk, 0, chunk.Length);
                    }

                    codec.ReleaseOutputBuffer(outputIndex, false);

                    bool isEos = (info.Flags & MediaCodecBufferFlags.EndOfStream) != 0;
                    if (isEos)
                    {
                        sawOutputEos = true;
                    }
                }
            }

            string extension = format == AudioExportFormat.Aac ? "aac" : "mp3";
            return await DerivedAudioFileWriter.SaveBytesAsync(encoded.ToArray(), ConvertedDirectory, outputName, extension);
        }
        finally
        {
            if (codec is not null)
            {
                try
                {
                    codec.Stop();
                }
                catch (Exception)
                {
                }

                codec.Release();
                codec.Dispose();
            }
        }
    }

    /// <summary>Writes a 7-byte ADTS header for one AAC frame directly to <paramref name="output"/>, the minimal
    /// framing MediaCodec's raw AAC output needs to become a standalone, playable .aac file.</summary>
    private static void WriteAdtsHeader(System.IO.Stream output, int aacFrameLength, int sampleRateIndex, int channelCount)
    {
        int frameLength = aacFrameLength + AdtsHeaderLength;
        byte[] header =
        [
            0xFF,
            0xF9, // MPEG-4, Layer 0, no CRC
            (byte)(((AacLcProfile - 1) << 6) | (sampleRateIndex << 2) | (channelCount >> 2)),
            (byte)(((channelCount & 3) << 6) | (frameLength >> 11)),
            (byte)((frameLength & 0x7FF) >> 3),
            (byte)(((frameLength & 7) << 5) | 0x1F),
            0xFC,
        ];

        output.Write(header, 0, header.Length);
    }
}
