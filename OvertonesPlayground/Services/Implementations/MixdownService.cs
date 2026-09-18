using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.Services.Implementations;

///<inheritdoc cref="IMixdownService"/>
public class MixdownService : IMixdownService
{
    #region Constants
    ///<summary>
    ///Every clip is conformed to this channel count before mixing, so per-track panning always has two channels to
    ///work with.
    ///</summary>
    private const int TargetChannels = 2;

    ///<summary>
    ///Every clip is resampled to this rate before mixing, matching the rate used elsewhere in the app (e.g.
    ///<c>SoundSynthesisService</c>).
    ///</summary>
    private const int TargetSampleRate = 44_100;
    #endregion

    #region Private methods
    ///<summary>
    ///Reads and conforms every clip on every included track, applying each clip's own gain, and returns each as a
    ///(samples, start frame, left gain, right gain) placement ready to accumulate into the mix buffer.
    ///</summary>
    private static async Task<List<(short[] Samples, int StartFrame, double LeftGain, double RightGain)>> LoadPlacementsAsync(IEnumerable<Track> includedTracks)
    {
        List<(short[] Samples, int StartFrame, double LeftGain, double RightGain)> placements = [];

        foreach (Track track in includedTracks)
        {
            double leftGain = track.Volume * (track.Pan <= 0 ? 1 : 1 - track.Pan);
            double rightGain = track.Volume * (track.Pan >= 0 ? 1 : 1 + track.Pan);

            foreach (TrackClip trackClip in track.Clips)
            {
                WavFile clipWav = await WavFile.ReadAsync(trackClip.ClipFilePath);
                short[] samples = AudioFormatUtility.Conform(clipWav, TargetChannels, TargetSampleRate);

                double clipFactor = Math.Pow(10, trackClip.GainDb / 20.0);
                bool hasClipGain = Math.Abs(clipFactor - 1.0) > double.Epsilon;
                if (hasClipGain)
                {
                    for (int i = 0; i < samples.Length; i++)
                    {
                        samples[i] = PcmMath.ClampToShort(samples[i] * clipFactor);
                    }
                }

                int startFrame = Math.Max(0, (int)Math.Round(trackClip.StartOffset.TotalSeconds * TargetSampleRate));
                placements.Add((samples, startFrame, leftGain, rightGain));
            }
        }

        return placements;
    }

    ///<summary>
    ///Additively accumulates every placement into a full-length mix buffer, sized to the latest-ending placement.
    ///</summary>
    private static double[] Accumulate(List<(short[] Samples, int StartFrame, double LeftGain, double RightGain)> placements)
    {
        int totalFrames = placements.Max(p => p.StartFrame + (p.Samples.Length / TargetChannels));
        double[] mixBuffer = new double[totalFrames * TargetChannels];

        foreach ((short[] samples, int startFrame, double leftGain, double rightGain) in placements)
        {
            int clipFrames = samples.Length / TargetChannels;
            for (int frame = 0; frame < clipFrames; frame++)
            {
                int outIndex = (startFrame + frame) * TargetChannels;
                mixBuffer[outIndex] += samples[frame * TargetChannels] * leftGain;
                mixBuffer[outIndex + 1] += samples[(frame * TargetChannels) + 1] * rightGain;
            }
        }

        return mixBuffer;
    }
    #endregion

    #region Private properties
    ///<summary>
    ///App-private folder where every mixdown's rendered output file is written.
    ///</summary>
    private static string MixesDirectory
    {
        get
        {
            string dir = Path.Combine(FileSystem.AppDataDirectory, "Mixes");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
    #endregion

    #region Public methods
    ///<inheritdoc/>
    public async Task<string> RenderAsync(MixProject project, string outputName)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputName);

        bool anySoloed = project.Tracks.Any(t => t.IsSoloed);
        IEnumerable<Track> includedTracks = project.Tracks.Where(t => anySoloed ? t.IsSoloed : !t.IsMuted);

        List<(short[] Samples, int StartFrame, double LeftGain, double RightGain)> placements = await LoadPlacementsAsync(includedTracks);

        bool hasNoPlacements = placements.Count == 0;
        if (hasNoPlacements)
        {
            throw new InvalidOperationException("Cannot render a mix project with no audible clips.");
        }

        double[] mixBuffer = Accumulate(placements);

        short[] output = new short[mixBuffer.Length];
        for (int i = 0; i < mixBuffer.Length; i++)
        {
            output[i] = PcmMath.ClampToShort(mixBuffer[i]);
        }

        WavFile mixed = new() { Channels = TargetChannels, SampleRate = TargetSampleRate, BitsPerSample = 16, Samples = output, };
        return await DerivedAudioFileWriter.SaveAsync(mixed, MixesDirectory, outputName);
    }
    #endregion
}
