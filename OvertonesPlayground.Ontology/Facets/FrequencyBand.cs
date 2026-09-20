namespace OvertonesPlayground.Ontology.Facets;

///<summary>
///The seven Hz-defined bands the spectral facet reports energy in. They are defined in hertz (not FFT bins), so files
///recorded at 22.05 kHz and 96 kHz are comparable.
///</summary>
public enum FrequencyBand
{
    ///<summary>Below 60 Hz.</summary>
    Sub,

    ///<summary>60 - 250 Hz.</summary>
    Bass,

    ///<summary>250 - 500 Hz.</summary>
    LowMid,

    ///<summary>500 Hz - 2 kHz.</summary>
    Mid,

    ///<summary>2 - 4 kHz.</summary>
    HighMid,

    ///<summary>4 - 8 kHz.</summary>
    Presence,

    ///<summary>Above 8 kHz.</summary>
    Air,
}

///<summary>
///Band edges and lookups for <see cref="FrequencyBand"/>.
///</summary>
public static class FrequencyBands
{
    #region Constants
    ///<summary>Number of bands.</summary>
    public const int Count = 7;

    private static readonly double[] _lowerEdgesHz = [0, 60, 250, 500, 2000, 4000, 8000];
    #endregion

    #region Public methods
    ///<summary>Band containing <paramref name="hz"/>.</summary>
    public static FrequencyBand FromFrequency(double hz)
    {
        for (int i = Count - 1; i > 0; i--)
        {
            if (hz >= _lowerEdgesHz[i])
            {
                return (FrequencyBand)i;
            }
        }

        return FrequencyBand.Sub;
    }

    ///<summary>Inclusive lower edge of <paramref name="band"/> in hertz.</summary>
    public static double LowerEdgeHz(FrequencyBand band) => _lowerEdgesHz[(int)band];

    ///<summary>Exclusive upper edge of <paramref name="band"/>; the last band ends at <paramref name="nyquistHz"/>.</summary>
    public static double UpperEdgeHz(FrequencyBand band, double nyquistHz)
    {
        int index = (int)band;
        bool isLast = index == Count - 1;
        return isLast ? nyquistHz : _lowerEdgesHz[index + 1];
    }
    #endregion
}
