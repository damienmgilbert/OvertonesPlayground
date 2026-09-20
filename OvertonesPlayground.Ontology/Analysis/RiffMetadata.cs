namespace OvertonesPlayground.Ontology.Analysis;

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
    ///<summary>No metadata.</summary>
    public static RiffMetadata Empty { get; } = new([], 0, null, null, null, null, new Dictionary<string, string>());
}
