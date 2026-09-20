namespace OvertonesPlayground.Ontology.Analysis;

///<summary>
///Decoded audio as one <see cref="float"/> array per channel, normalized to -1 .. 1. Unlike the app's
///<c>short[]</c>-based reader this keeps full 24-bit and float precision.
///</summary>
public sealed class AudioBuffer
{
    #region Constructors
    ///<summary>Wraps decoded channels; every channel must have the same length.</summary>
    public AudioBuffer(int sampleRate, float[][] channels)
    {
        bool isRagged = channels.Length > 1 && channels.Any(channel => channel.Length != channels[0].Length);
        if (isRagged)
        {
            throw new ArgumentException("All channels must have the same length.", nameof(channels));
        }

        SampleRate = sampleRate;
        Channels = channels;
    }
    #endregion

    #region Public methods
    ///<summary>Average of all channels.</summary>
    public float[] MixToMono()
    {
        if (Channels.Length == 1)
        {
            return Channels[0];
        }

        float[] mono = new float[FrameCount];
        float scale = 1f / Channels.Length;
        foreach (float[] channel in Channels)
        {
            for (int i = 0; i < mono.Length; i++)
            {
                mono[i] += channel[i] * scale;
            }
        }

        return mono;
    }
    #endregion

    #region Public properties
    ///<summary>The samples of each channel.</summary>
    public float[][] Channels { get; }

    ///<summary>Number of channels.</summary>
    public int ChannelCount => Channels.Length;

    ///<summary>Length in seconds.</summary>
    public double DurationSeconds => SampleRate == 0 ? 0 : (double)FrameCount / SampleRate;

    ///<summary>Samples per channel.</summary>
    public int FrameCount => Channels.Length == 0 ? 0 : Channels[0].Length;

    ///<summary>Samples per second.</summary>
    public int SampleRate { get; }
    #endregion
}
