namespace OvertonesPlayground.Services.Implementations;

///<summary>
///An iterative radix-2 Cooley-Tukey FFT/IFFT over parallel real/imaginary buffers, in place. The only spectral
///primitive in the app; <c>SpectralNoiseReducer</c> is its first consumer, and the equalizer could reuse it
///later for a frequency-response preview instead of doing the DSP itself.
///</summary>
internal static class FastFourierTransform
{
    #region Private methods

    ///<summary>
    ///Reorders <paramref name="real"/>/<paramref name="imaginary"/> into bit-reversed index order, the standard first
    ///pass of an in-place iterative FFT.
    ///</summary>
    private static void BitReverse(Span<double> real, Span<double> imaginary)
    {
        int n = real.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1)
            {
                j ^= bit;
            }

            j ^= bit;
            if (i < j)
            {
                (real[i], real[j]) = (real[j], real[i]);
                (imaginary[i], imaginary[j]) = (imaginary[j], imaginary[i]);
            }
        }
    }

    ///<summary>
    ///Runs the forward or inverse transform, in place. <paramref name="real"/>.Length must be a power of two.
    ///</summary>
    private static void Transform(Span<double> real, Span<double> imaginary, bool inverse)
    {
        int n = real.Length;
        BitReverse(real, imaginary);

        for (int length = 2; length <= n; length <<= 1)
        {
            double angle = (inverse ? 1 : -1) * 2 * Math.PI / length;
            double wReal = Math.Cos(angle);
            double wImag = Math.Sin(angle);

            for (int start = 0; start < n; start += length)
            {
                double stepReal = 1;
                double stepImag = 0;

                for (int k = 0; k < length / 2; k++)
                {
                    int evenIndex = start + k;
                    int oddIndex = start + k + (length / 2);

                    double evenReal = real[evenIndex];
                    double evenImag = imaginary[evenIndex];
                    double oddReal = (real[oddIndex] * stepReal) - (imaginary[oddIndex] * stepImag);
                    double oddImag = (real[oddIndex] * stepImag) + (imaginary[oddIndex] * stepReal);

                    real[evenIndex] = evenReal + oddReal;
                    imaginary[evenIndex] = evenImag + oddImag;
                    real[oddIndex] = evenReal - oddReal;
                    imaginary[oddIndex] = evenImag - oddImag;

                    double nextStepReal = (stepReal * wReal) - (stepImag * wImag);
                    double nextStepImag = (stepReal * wImag) + (stepImag * wReal);
                    stepReal = nextStepReal;
                    stepImag = nextStepImag;
                }
            }
        }

        if (inverse)
        {
            for (int i = 0; i < n; i++)
            {
                real[i] /= n;
                imaginary[i] /= n;
            }
        }
    }
    #endregion

    #region Public methods
    ///<summary>
    ///Transforms a time-domain signal into its frequency-domain spectrum, in place.
    ///</summary>
    public static void Forward(Span<double> real, Span<double> imaginary) => Transform(real, imaginary, inverse: false);

    ///<summary>
    ///Transforms a frequency-domain spectrum back into a time-domain signal, in place.
    ///</summary>
    public static void Inverse(Span<double> real, Span<double> imaginary) => Transform(real, imaginary, inverse: true);
    #endregion
}
