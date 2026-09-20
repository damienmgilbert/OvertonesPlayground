namespace OvertonesPlayground.Ontology.Facets;

///<summary>
///Spectral content, measured over the active part of the sample with energy-weighted frames.
///</summary>
///<param name="CentroidHz">Power-weighted mean frequency (brightness).</param>
///<param name="RolloffHz">Frequency below which 85 % of the energy lies.</param>
///<param name="BandwidthHz">Power-weighted standard deviation around the centroid.</param>
///<param name="Flatness">Geometric / arithmetic mean of the magnitude spectrum: 0 = a pure tone, 1 = white noise.</param>
///<param name="Flux">Mean normalized frame-to-frame spectral change (how quickly the spectrum evolves).</param>
///<param name="TiltDbPerOctave">Slope of the magnitude spectrum in dB per octave (negative = darker).</param>
///<param name="Bands">Energy share per <see cref="FrequencyBand"/>.</param>
///<param name="OnsetCentroidHz">Centroid of the first ~23 ms of the sound, where the attack transient lives; compared with <paramref name="CentroidHz"/> it says how quickly a sound darkens.</param>
///<param name="Mfcc">Mel-frequency cepstral coefficients 1 - 12: a compact, level-independent description of the spectral envelope (timbre).</param>
public sealed record SpectralFacet(
    double CentroidHz,
    double RolloffHz,
    double BandwidthHz,
    double Flatness,
    double Flux,
    double TiltDbPerOctave,
    BandEnergies Bands,
    double OnsetCentroidHz,
    double[] Mfcc);
