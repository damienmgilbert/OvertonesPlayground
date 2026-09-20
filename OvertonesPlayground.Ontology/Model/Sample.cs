using System.Text.Json.Serialization;

namespace OvertonesPlayground.Ontology.Model;

///<summary>
///The aggregate root of the ontology: one bundled audio file, described by an asset identity, one facet per acoustic
///category, and a classification. Immutable.
///</summary>
///<param name="Asset">Identity of the file.</param>
///<param name="Status">Whether analysis succeeded.</param>
///<param name="Technical">Format facts; null only when the file could not be read at all.</param>
///<param name="Spectral">Spectral content, or null when not analysed.</param>
///<param name="Tonality">Sound character and pitch, or null when not analysed.</param>
///<param name="Dynamics">Amplitude, loudness and envelope, or null when not analysed.</param>
///<param name="Stereo">Stereo field and phase, or null when not analysed.</param>
///<param name="Rhythm">Tempo and onsets, or null when not analysed or too short.</param>
///<param name="Classification">Instrument, kit, style, content type and origin.</param>
///<param name="Notes">Analysis remarks worth a human look (truncated data, silence, an analyzer that failed); null when there are none.</param>
public sealed record Sample(
    SampleAsset Asset,
    AnalysisStatus Status,
    TechnicalFacet? Technical,
    SpectralFacet? Spectral,
    TonalityFacet? Tonality,
    DynamicsFacet? Dynamics,
    StereoFacet? Stereo,
    RhythmFacet? Rhythm,
    SampleClassification Classification,
    IReadOnlyList<string>? Notes = null)
{
    #region Public properties
    ///<summary>Stable key: the asset file name.</summary>
    [JsonIgnore]
    public string Id => Asset.FileName;

    ///<summary>Display name (file name without extension).</summary>
    [JsonIgnore]
    public string Name => Asset.DisplayName;

    ///<summary>
    ///Tempo to use: the tempo in the name when present (people wrote it deliberately), otherwise the detected tempo when its
    ///periodicity is strong enough to believe (<see cref="RhythmFacet.TrustedBpm"/>), otherwise none.
    ///</summary>
    [JsonIgnore]
    public double? EffectiveTempoBpm => Classification.Attributes.TempoBpm ?? Rhythm?.TrustedBpm;

    ///<summary>
    ///Pitch class (0 = C) to use: the key written in the name when present, otherwise the detected pitch.
    ///</summary>
    [JsonIgnore]
    public int? EffectivePitchClass => Classification.Attributes.KeyPitchClass ?? Tonality?.PitchClass;
    #endregion
}
