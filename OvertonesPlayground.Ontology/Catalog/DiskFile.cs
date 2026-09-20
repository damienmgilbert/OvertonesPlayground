namespace OvertonesPlayground.Ontology.Catalog;

///<summary>
///An audio file found on disk, as far as planning an update needs to know it.
///</summary>
///<param name="Name">File name with extension; the sample id.</param>
///<param name="SizeBytes">Size of the file.</param>
public readonly record struct DiskFile(string Name, long SizeBytes);
