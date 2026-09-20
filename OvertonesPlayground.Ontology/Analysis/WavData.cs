namespace OvertonesPlayground.Ontology.Analysis;

///<summary>
///How the samples were stored in the file.
///</summary>
///<param name="Channels">Channel count.</param>
///<param name="SampleRate">Samples per second.</param>
///<param name="BitsPerSample">Container bit depth (8, 16, 24, 32 or 64).</param>
///<param name="IsFloat">IEEE float rather than integer PCM.</param>
///<param name="IsExtensible">Stored as <c>WAVE_FORMAT_EXTENSIBLE</c>.</param>
///<param name="WasTruncated">The <c>data</c> chunk declared more bytes than the file holds.</param>
public sealed record WavFormat(int Channels, int SampleRate, int BitsPerSample, bool IsFloat, bool IsExtensible, bool WasTruncated)
{
    #region Public properties
    ///<summary><c>PCM</c>, <c>PCM-Extensible</c> or <c>Float</c>, as stored in the technical facet.</summary>
    public string EncodingName => IsFloat ? "Float" : IsExtensible ? "PCM-Extensible" : "PCM";
    #endregion
}

///<summary>
///Optional information found in sampler and broadcast chunks. Treated as weak evidence: many files carry sampler
///defaults (unity note 60, tempo 0) rather than real values.
///</summary>
///<param name="ChunkIds">Four-character ids of every chunk, in file order.</param>
///<param name="SampleLoopCount">Loops declared in the <c>smpl</c> chunk.</param>
///<param name="SmplUnityNote">MIDI unity note from <c>smpl</c>.</param>
///<param name="AcidTempo">Tempo from the <c>acid</c> chunk when plausible (20 - 400 BPM).</param>
///<param name="AcidRootNote">Root note from the <c>acid</c> chunk.</param>
///<param name="AcidIsOneShot">The <c>acid</c> chunk flags the file as a one-shot.</param>
///<param name="InfoTags"><c>LIST/INFO</c> text tags such as <c>IGNR</c> (genre) or <c>ICMT</c> (comment).</param>
public sealed record RiffMetadata(
    IReadOnlyList<string> ChunkIds,
    int SampleLoopCount,
    int? SmplUnityNote,
    double? AcidTempo,
    int? AcidRootNote,
    bool? AcidIsOneShot,
    IReadOnlyDictionary<string, string> InfoTags)
{
    #region Public properties
    ///<summary>No metadata.</summary>
    public static RiffMetadata Empty { get; } = new([], 0, null, null, null, null, new Dictionary<string, string>());
    #endregion
}

///<summary>
///A decoded WAV file.
///</summary>
///<param name="Audio">The samples.</param>
///<param name="Metadata">Embedded chunks.</param>
///<param name="Format">Storage format.</param>
public sealed record WavData(AudioBuffer Audio, RiffMetadata Metadata, WavFormat Format);
