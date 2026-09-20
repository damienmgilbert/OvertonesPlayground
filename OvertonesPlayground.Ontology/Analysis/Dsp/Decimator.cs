namespace OvertonesPlayground.Ontology.Analysis.Dsp;

///<summary>
///Sample-rate reduction for analyses (pitch, rhythm) that do not need the full bandwidth.
///</summary>
public static class Decimator
{
    #region Public methods
    ///<summary>
    ///Low-passes with two cascaded sections at 0.4 of the new sample rate, then keeps every ///<paramref
    ///name="factor"/>-th sample. A factor of 1 returns a copy.
    ///</summary>
    public static float[] Decimate(ReadOnlySpan<float> input, int factor, int sampleRate)
    {
        if (factor <= 1)
        {
            return input.ToArray();
        }

        double cutoff = 0.4 * sampleRate / factor;
        float[] filtered = Biquad.LowPass(sampleRate, cutoff).Process(Biquad.LowPass(sampleRate, cutoff).Process(input));
        float[] output = new float[filtered.Length / factor];
        for (int i = 0; i < output.Length; i++)
        {
            output[i] = filtered[i * factor];
        }

        return output;
    }

    ///<summary>
    ///Picks the integer factor that brings <paramref name="sampleRate"/> closest to <paramref name="targetRate"/> (44.1
    ///kHz and 48 kHz become about 22 kHz, 96 kHz becomes 24 kHz).
    ///</summary>
    public static int FactorFor(int sampleRate, double targetRate) => Math.Max(1, (int)Math.Round(sampleRate / targetRate));
    #endregion
}
