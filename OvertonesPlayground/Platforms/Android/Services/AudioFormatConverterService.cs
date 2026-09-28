using System.Diagnostics;
using Android.Media;
using Java.Nio;
using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Implementations;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.Platforms.Android.Services;

/// <summary>
/// Converts between 16-bit PCM WAV and compressed audio using Android's <see cref="MediaExtractor"/>/
/// <see cref="MediaCodec"/>: decoding MP3/AAC/OGG/MP4/etc. into WAV so the app's WAV-only pipeline can read them
/// (pulling the audio track straight out of a video container works the same way, since only the first audio track is
/// ever selected), and encoding WAV to AAC/MP3 for export. Both directions stream to/from disk in bounded chunks
/// rather than buffering a whole file in memory, since a source can be a multi-hour mixtape - hundreds of megabytes
/// of raw PCM once decoded.
/// </summary>
public class AudioFormatConverterService : IAudioFormatConverterService
{
    /// <summary>7-byte ADTS header length - the minimal framing MediaCodec's raw AAC output needs to become a
    /// standalone, playable .aac file without a full MP4/M4A muxer.</summary>
    private const int AdtsHeaderLength = 7;

    /// <summary>The MPEG-4 Audio Object Type for AAC-LC, per the ADTS header spec's profile field (encoded as
    /// profile-1).</summary>
    private const int AacLcProfile = 2;

    private const long DequeueTimeoutUs = 10_000;

    /// <summary>The fewest milliseconds between two progress reports.</summary>
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
    public Task<string> ConvertFromWavAsync(string sourcePath, AudioExportFormat format, string outputName, int bitRateBps = 128_000, IProgress<double>? progress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputName);

        return Task.Run(() => EncodeFromWavAsync(sourcePath, format, outputName, bitRateBps, progress));
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

    /// <inheritdoc />
    public Task<TimeSpan> ProbeDurationAsync(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        return Task.Run(() => ProbeDuration(sourcePath));
    }

    /// <summary>Opens just <paramref name="sourcePath"/>'s container metadata to read its audio track's duration,
    /// without decoding any of it.</summary>
    private static TimeSpan ProbeDuration(string sourcePath)
    {
        MediaExtractor extractor = new();
        try
        {
            extractor.SetDataSource(sourcePath);

            for (int i = 0; i < extractor.TrackCount; i++)
            {
                MediaFormat format = extractor.GetTrackFormat(i);
                string? mime = format.GetString(MediaFormat.KeyMime);
                bool isAudioTrack = mime?.StartsWith("audio/", StringComparison.Ordinal) == true;
                if (isAudioTrack)
                {
                    long durationUs = format.ContainsKey(MediaFormat.KeyDuration) ? format.GetLong(MediaFormat.KeyDuration) : 0;
                    return TimeSpan.FromMicroseconds(durationUs);
                }
            }

            throw new NotSupportedException($"'{sourcePath}' has no audio track.");
        }
        finally
        {
            extractor.Release();
            extractor.Dispose();
        }
    }

    /// <summary>Tells <paramref name="progress"/> how far <paramref name="positionUs"/> is through
    /// <paramref name="durationUs"/>, but no more often than <see cref="ProgressIntervalMs"/> so a long decode doesn't flood
    /// the UI thread with updates.</summary>
    private static void ReportDecodeProgress(IProgress<double>? progress, long positionUs, long durationUs, Stopwatch clock)
    {
        if (durationUs <= 0)
        {
            return;
        }

        ReportThrottledProgress(progress, (double)positionUs / durationUs, clock);
    }

    /// <summary>Shared throttle behind <see cref="ReportDecodeProgress"/> and the encode loop's byte-based progress:
    /// reports <paramref name="fraction"/>, clamped to [0, 1], but no more often than <see cref="ProgressIntervalMs"/>.</summary>
    private static void ReportThrottledProgress(IProgress<double>? progress, double fraction, Stopwatch clock)
    {
        bool cannotReport = progress is null || clock.ElapsedMilliseconds < ProgressIntervalMs;
        if (cannotReport)
        {
            return;
        }

        clock.Restart();
        progress!.Report(Math.Clamp(fraction, 0, 1));
    }

    /// <summary>Reads up to <paramref name="count"/> bytes from <paramref name="stream"/> into <paramref name="buffer"/>,
    /// looping until either that many bytes have been read or the stream ends. A local file stream's <c>Read</c> can
    /// legally return fewer bytes than asked for even before end-of-stream, so a single <c>Read</c> call isn't enough
    /// to trust the buffer is full.</summary>
    private static int ReadFully(System.IO.Stream stream, byte[] buffer, int count)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = stream.Read(buffer, totalRead, count - totalRead);
            if (read <= 0)
            {
                break;
            }

            totalRead += read;
        }

        return totalRead;
    }

    /// <summary>Runs the blocking extractor/decoder pump loop, streaming the result straight to a WAV file on disk
    /// instead of buffering it in memory.</summary>
    private static async Task<string> DecodeToWavAsync(string sourcePath, string outputName, IProgress<double>? progress)
    {
        string outputPath = DerivedAudioFileWriter.ReservePath(ConvertedDirectory, outputName, "wav");
        MediaExtractor extractor = new();
        MediaCodec? codec = null;
        WavStream.Writer? wavWriter = null;
        bool completed = false;
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
                        // Deferred until the first real output: Android emits OUTPUT_FORMAT_CHANGED (if at all) before
                        // any actual audio, so channels/sampleRate are final by the time there's anything to write.
                        wavWriter ??= WavStream.CreateWriter(outputPath, (short)channels, sampleRate);

                        ByteBuffer outputBuffer = codec.GetOutputBuffer(outputIndex)!;
                        byte[] chunk = new byte[info.Size];
                        outputBuffer.Position(info.Offset);
                        outputBuffer.Limit(info.Offset + info.Size);
                        outputBuffer.Get(chunk);
                        await wavWriter.WriteAsync(chunk, chunk.Length);
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

            // The track produced no audio at all (e.g. a zero-length track) - still write a valid, empty WAV rather
            // than leaving nothing on disk.
            wavWriter ??= WavStream.CreateWriter(outputPath, (short)channels, sampleRate);
            await wavWriter.CompleteAsync();
            completed = true;
            return outputPath;
        }
        finally
        {
            wavWriter?.Dispose();
            if (wavWriter is not null && !completed)
            {
                // The decode failed partway through - what's on disk is a truncated, header-only WAV, not a usable
                // partial result, so don't leave it behind.
                TryDelete(outputPath);
            }

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

    /// <summary>Best-effort delete; a failed conversion's own cleanup shouldn't throw a second exception over it.</summary>
    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>Runs the blocking PCM-in/compressed-out encoder pump loop, streaming the source WAV's data chunk
    /// straight from disk and the encoded result straight to disk, so neither has to fit in memory at once.</summary>
    private static async Task<string> EncodeFromWavAsync(string sourcePath, AudioExportFormat format, string outputName, int bitRateBps, IProgress<double>? progress)
    {
        using WavStream.Reader source = WavStream.OpenDataReader(sourcePath);
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

        string extension = format == AudioExportFormat.Aac ? "aac" : "mp3";
        string outputPath = DerivedAudioFileWriter.ReservePath(ConvertedDirectory, outputName, extension);
        bool completed = false;

        MediaCodec? codec = null;
        FileStream? destination = null;
        try
        {
            using MediaFormat outputFormat = MediaFormat.CreateAudioFormat(mimeType, source.SampleRate, source.Channels);
            outputFormat.SetInteger(MediaFormat.KeyBitRate, bitRateBps);
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
            destination = File.Create(outputPath);

            using MediaCodec.BufferInfo info = new();
            long bytesRead = 0;
            bool sawInputEos = false;
            bool sawOutputEos = false;
            Stopwatch progressClock = Stopwatch.StartNew();

            while (!sawOutputEos)
            {
                if (!sawInputEos)
                {
                    int inputIndex = codec.DequeueInputBuffer(DequeueTimeoutUs);
                    if (inputIndex >= 0)
                    {
                        ByteBuffer inputBuffer = codec.GetInputBuffer(inputIndex)!;
                        inputBuffer.Clear();
                        int toRead = (int)Math.Min(inputBuffer.Capacity(), source.DataLength - bytesRead);
                        bool isDone = toRead <= 0;
                        if (isDone)
                        {
                            codec.QueueInputBuffer(inputIndex, 0, 0, 0, MediaCodecBufferFlags.EndOfStream);
                            sawInputEos = true;
                        }
                        else
                        {
                            byte[] chunk = new byte[toRead];
                            int actuallyRead = ReadFully(source.Data, chunk, toRead);
                            if (actuallyRead <= 0)
                            {
                                // The file is shorter than its header claimed - end the input here rather than spin.
                                codec.QueueInputBuffer(inputIndex, 0, 0, 0, MediaCodecBufferFlags.EndOfStream);
                                sawInputEos = true;
                            }
                            else
                            {
                                inputBuffer.Put(chunk, 0, actuallyRead);
                                codec.QueueInputBuffer(inputIndex, 0, actuallyRead, 0, MediaCodecBufferFlags.None);
                                bytesRead += actuallyRead;
                                ReportThrottledProgress(progress, (double)bytesRead / source.DataLength, progressClock);
                            }
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
                            WriteAdtsHeader(destination, chunk.Length, adtsFrequencyIndex, source.Channels);
                        }

                        await destination.WriteAsync(chunk);
                    }

                    codec.ReleaseOutputBuffer(outputIndex, false);

                    bool isEos = (info.Flags & MediaCodecBufferFlags.EndOfStream) != 0;
                    if (isEos)
                    {
                        sawOutputEos = true;
                    }
                }
            }

            await destination.FlushAsync();
            completed = true;
            return outputPath;
        }
        finally
        {
            destination?.Dispose();
            if (destination is not null && !completed)
            {
                TryDelete(outputPath);
            }

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
