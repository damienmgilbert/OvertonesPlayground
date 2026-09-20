using System.Text.Json.Serialization;

namespace OvertonesPlayground.Ontology.Facets;

///<summary>
///Share of the spectral energy of a sample that falls in each <see cref="FrequencyBand"/>. The seven values sum to 1
///(or are all zero for digital silence).
///</summary>
///<param name="Sub">Fraction below 60 Hz.</param>
///<param name="Bass">Fraction 60 - 250 Hz.</param>
///<param name="LowMid">Fraction 250 - 500 Hz.</param>
///<param name="Mid">Fraction 500 Hz - 2 kHz.</param>
///<param name="HighMid">Fraction 2 - 4 kHz.</param>
///<param name="Presence">Fraction 4 - 8 kHz.</param>
///<param name="Air">Fraction above 8 kHz.</param>
public sealed record BandEnergies(double Sub, double Bass, double LowMid, double Mid, double HighMid, double Presence, double Air)
{
    #region Public properties
    ///<summary>The band holding the most energy.</summary>
    [JsonIgnore]
    public FrequencyBand Dominant
    {
        get
        {
            double[] values = ToArray();
            int best = 0;
            for (int i = 1; i < values.Length; i++)
            {
                if (values[i] > values[best])
                {
                    best = i;
                }
            }

            return (FrequencyBand)best;
        }
    }

    ///<summary>Energy fraction of <paramref name="band"/>.</summary>
    public double this[FrequencyBand band] => ToArray()[(int)band];
    #endregion

    #region Public methods
    ///<summary>Builds an instance from seven values in <see cref="FrequencyBand"/> order.</summary>
    public static BandEnergies FromArray(ReadOnlySpan<double> values) =>
        new(values[0], values[1], values[2], values[3], values[4], values[5], values[6]);

    ///<summary>The seven fractions in <see cref="FrequencyBand"/> order.</summary>
    public double[] ToArray() => [Sub, Bass, LowMid, Mid, HighMid, Presence, Air];
    #endregion
}
