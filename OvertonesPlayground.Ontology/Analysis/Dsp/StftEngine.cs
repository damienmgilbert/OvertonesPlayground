using OvertonesPlayground.Services.Implementations;

namespace OvertonesPlayground.Ontology.Analysis.Dsp;

///<summary>
///Windowed short-time spectra on top of the app's radix-2 <see cref="FastFourierTransform"/>. One engine owns its
///scratch buffers, so create one per analysis; it is not thread-safe.
///</summary>
public sealed class StftEngine
{
    #region Fields
    private readonly double[] _imaginary;
    private readonly double[] _real;
    private readonly double[] _window;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates an engine for frames of <paramref name="fftSize"/> samples (a power of two).
    ///</summary>
    public StftEngine(int fftSize)
    {
        bool isPowerOfTwo = fftSize >= 2 && (fftSize & (fftSize - 1)) == 0;
        if (!isPowerOfTwo)
        {
            throw new ArgumentException("The FFT size must be a power of two.", nameof(fftSize));
        }

        FftSize = fftSize;
        _real = new double[fftSize];
        _imaginary = new double[fftSize];
        _window = new double[fftSize];
        for (int i = 0; i < fftSize; i++)
        {
            _window[i] = 0.5 - (0.5 * Math.Cos(2.0 * Math.PI * i / fftSize));
        }
    }
    #endregion

    #region Public methods
    ///<summary>
    ///Frequency of bin <paramref name="bin"/> for the given sample rate.
    ///</summary>
    public double BinFrequency(int bin, int sampleRate) => (double)bin * sampleRate / FftSize;

    ///<summary>
    ///Computes the Hann-windowed power spectrum (|X|^2, bins 0 .. N/2) of the frame starting at ///<paramref
    ///name="start"/>. Samples beyond the end of <paramref name="signal"/> count as zero.
    ///</summary>
    public void Power(ReadOnlySpan<float> signal, int start, Span<double> destination)
    {
        for (int i = 0; i < FftSize; i++)
        {
            int index = start + i;
            bool isInside = index >= 0 && index < signal.Length;
            _real[i] = isInside ? signal[index] * _window[i] : 0.0;
            _imaginary[i] = 0.0;
        }

        FastFourierTransform.Forward(_real, _imaginary);
        for (int bin = 0; bin < BinCount; bin++)
        {
            destination[bin] = (_real[bin] * _real[bin]) + (_imaginary[bin] * _imaginary[bin]);
        }
    }
    #endregion

    #region Public properties
    ///<summary>
    ///Number of spectrum bins, N/2 + 1.
    ///</summary>
    public int BinCount => (FftSize / 2) + 1;

    ///<summary>
    ///Frame length in samples.
    ///</summary>
    public int FftSize { get; }
    #endregion
}
