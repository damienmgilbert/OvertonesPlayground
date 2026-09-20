using System.Text.Json.Serialization;

namespace OvertonesPlayground.Ontology.Model;

///<summary>
///Identity of a bundled audio file. The file name is the stable key: it is the logical name of the MAUI asset and never
///changes between builds (unlike a library clip id, which is a random GUID).
///</summary>
///<param name="FileName">File name with extension; also the asset name passed to <c>OpenAppPackageFileAsync</c>.</param>
///<param name="SizeBytes">File size in bytes.</param>
///<param name="ContentHash">SHA-256 of the file, lower-case hex; used to skip unchanged files on re-analysis.</param>
public sealed record SampleAsset(string FileName, long SizeBytes, string ContentHash)
{
    #region Public properties

    ///<summary>
    ///File name without extension; the display name.
    ///</summary>
    [JsonIgnore]
    public string DisplayName => Path.GetFileNameWithoutExtension(FileName);
    #endregion
}
