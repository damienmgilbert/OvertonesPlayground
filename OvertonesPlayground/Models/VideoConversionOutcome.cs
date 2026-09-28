namespace OvertonesPlayground.Models;

///<summary>
///The result of converting a picked video's audio track.
///</summary>
///<param name="FileName">The name of the converted file.</param>
///<param name="PublicLocation">A short, human-readable location the file was saved to (e.g.
///"Music/OvertonesPlayground/song.mp3"), or null if the file was converted but couldn't be saved to shared storage.</param>
public readonly record struct VideoConversionOutcome(string FileName, string? PublicLocation);
