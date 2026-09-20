namespace OvertonesPlayground.Ontology.Analysis;

///<summary>
///A decoded WAV file.
///</summary>
///<param name="Audio">The samples.</param>
///<param name="Metadata">Embedded chunks.</param>
///<param name="Format">Storage format.</param>
public sealed record WavData(AudioBuffer Audio, RiffMetadata Metadata, WavFormat Format);
