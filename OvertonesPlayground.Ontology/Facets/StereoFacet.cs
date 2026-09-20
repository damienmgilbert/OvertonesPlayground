namespace OvertonesPlayground.Ontology.Facets;

///<summary>
///Stereo field and phase behaviour. For mono files every value is neutral and <paramref name="Image"/> is
///<see cref="StereoImage.Mono"/>.
///</summary>
///<param name="Channels">Channel count of the file.</param>
///<param name="Image">Classification of the stereo image.</param>
///<param name="Correlation">Pearson correlation of left and right, -1 (inverted) to 1 (identical).</param>
///<param name="LowBandCorrelation">Same, but only below 150 Hz (bass that is not mono-compatible is a mixing problem).</param>
///<param name="Width">Side energy divided by mid + side energy: 0 = mono, about 0.5 = uncorrelated, 1 = inverted.</param>
///<param name="BalanceDb">Left minus right energy in dB (positive = left heavy).</param>
///<param name="Pan">Energy centre, -1 (hard left) to 1 (hard right).</param>
///<param name="MonoCompatibilityDb">Level change when summing to mono: 0 = lossless, more negative = more cancellation.</param>
///<param name="PolarityInverted">The channels are predominantly polarity-inverted.</param>
public sealed record StereoFacet(
    int Channels,
    StereoImage Image,
    double Correlation,
    double LowBandCorrelation,
    double Width,
    double BalanceDb,
    double Pan,
    double MonoCompatibilityDb,
    bool PolarityInverted);
