using System.Text.Json.Serialization;

namespace OvertonesPlayground.Ontology.Facets;

///<summary>
///Container-level facts about the file: format, size and embedded sampler metadata.
///</summary>
///<param name="SampleRate">Samples per second per channel.</param>
///<param name="BitsPerSample">Bit depth of the stored PCM / float samples.</param>
///<param name="Channels">Channel count.</param>
///<param name="DurationSeconds">Length of the audio actually present.</param>
///<param name="FileSizeBytes">Size of the file on disk.</param>
///<param name="Encoding"><c>PCM</c>, <c>PCM-Extensible</c> or <c>Float</c>.</param>
///<param name="WasTruncated">The <c>data</c> chunk declared more bytes than the file contains.</param>
///<param name="LoopPointCount">Sampler loops declared in the <c>smpl</c> chunk.</param>
///<param name="EmbeddedTempoBpm">Tempo from an <c>acid</c> chunk on a non-one-shot file; many one-shots carry junk tempos, so those are ignored.</param>
///<param name="EmbeddedGenre">Genre text from a <c>LIST/INFO</c><c>IGNR</c> tag, if any.</param>
public sealed record TechnicalFacet(int SampleRate, int BitsPerSample, int Channels, double DurationSeconds, long FileSizeBytes, string Encoding, bool WasTruncated, int LoopPointCount, double? EmbeddedTempoBpm, string? EmbeddedGenre)
{
    #region Public properties

    ///<summary>
    ///Length bucket for browsing.
    ///</summary>
    [JsonIgnore]
    public LengthClass Length => DurationSeconds switch
    {
        < 0.25 => LengthClass.Hit,
        < 1.0 => LengthClass.Short,
        < 4.0 => LengthClass.Medium,
        < 15.0 => LengthClass.Long,
        _ => LengthClass.Extended,
    };
    #endregion
}
