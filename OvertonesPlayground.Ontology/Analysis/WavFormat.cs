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
    ///<summary><c>PCM</c>, <c>PCM-Extensible</c> or <c>Float</c>, as stored in the technical facet.</summary>
    public string EncodingName => IsFloat ? "Float" : IsExtensible ? "PCM-Extensible" : "PCM";
}
