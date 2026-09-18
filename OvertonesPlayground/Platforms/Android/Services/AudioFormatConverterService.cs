using Android.Media;
using Java.Nio;
using OvertonesPlayground.Services.Implementations;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.Platforms.Android.Services;

/// <summary>
/// Decodes MP3/AAC/OGG/etc. into 16-bit PCM WAV using Android's <see cref="MediaExtractor"/> and
/// <see cref="MediaCodec"/>, so files the app's WAV-only pipeline can't read directly can still be imported.
/// </summary>
public class AudioFormatConverterService : IAudioFormatConverterService
{
    private const long DequeueTimeoutUs = 10_000;

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
    public Task<string> ConvertToWavAsync(string sourcePath, string outputName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputName);

        return Task.Run(() => DecodeToWavAsync(sourcePath, outputName));
    }

    /// <inheritdoc />
    public bool NeedsConversion(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return !string.Equals(Path.GetExtension(filePath), ".wav", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Runs the blocking extractor/decoder pump loop and writes the result as a WAV file.</summary>
    private static async Task<string> DecodeToWavAsync(string sourcePath, string outputName)
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
                            codec.QueueInputBuffer(inputIndex, 0, sampleSize, extractor.SampleTime, MediaCodecBufferFlags.None);
                            extractor.Advance();
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
}
