namespace OvertonesPlayground.Ontology.Analysis.Dsp;

///<summary>
///Conversions between linear values and decibels with a floor, so silence never produces -infinity in the catalog.
///</summary>
public static class Decibels
{
    #region Constants

    ///<summary>
    ///The lowest value reported (digital silence).
    ///</summary>
    public const double Floor = -120.0;
    #endregion

    #region Public methods
    ///<summary>
    ///20 log10 of an amplitude, never below <see cref="Floor"/>.
    ///</summary>
    public static double FromAmplitude(double amplitude) => amplitude <= 1e-6 ? Floor : Math.Max(Floor, 20.0 * Math.Log10(amplitude));

    ///<summary>
    ///10 log10 of a power, never below <see cref="Floor"/>.
    ///</summary>
    public static double FromPower(double power) => power <= 1e-12 ? Floor : Math.Max(Floor, 10.0 * Math.Log10(power));

    ///<summary>
    ///Rounds to the given number of decimals; keeps catalog numbers short and diffs stable.
    ///</summary>
    public static double Round(double value, int digits = 3) => double.IsFinite(value) ? Math.Round(value, digits, MidpointRounding.AwayFromZero) : 0.0;

    ///<summary>
    ///Amplitude ratio for <paramref name="decibels"/>.
    ///</summary>
    public static double ToAmplitude(double decibels) => Math.Pow(10.0, decibels / 20.0);
    #endregion
}
