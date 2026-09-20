namespace OvertonesPlayground.Ontology.Facets;

///<summary>
///Band edges and lookups for <see cref="FrequencyBand"/>.
///</summary>
public static class FrequencyBands
{
    #region Constants

    ///<summary>
    ///Number of bands.
    ///</summary>
    public const int Count = 7;
    #endregion

    #region Fields
    private static readonly double[] _lowerEdgesHz = [0, 60, 250, 500, 2000, 4000, 8000];
    #endregion

    #region Public methods
    ///<summary>
    ///Band containing <paramref name="hz"/>.
    ///</summary>
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

    ///<summary>
    ///Inclusive lower edge of <paramref name="band"/> in hertz.
    ///</summary>
    public static double LowerEdgeHz(FrequencyBand band) => _lowerEdgesHz[(int)band];

    ///<summary>
    ///Exclusive upper edge of <paramref name="band"/>; the last band ends at <paramref name="nyquistHz"/>.
    ///</summary>
    public static double UpperEdgeHz(FrequencyBand band, double nyquistHz)
    {
        int index = (int)band;
        bool isLast = index == Count - 1;
        return isLast ? nyquistHz : _lowerEdgesHz[index + 1];
    }
    #endregion
}
