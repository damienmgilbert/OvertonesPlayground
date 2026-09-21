namespace OvertonesPlayground.Models;

///<summary>
///A sound of the Sound Bank suggested for a pad, and why.
///</summary>
///<param name="SampleName">The sound's name in the Sound Bank, without the file extension.</param>
///<param name="Reasons">Why it suits the pad, in a few words each ("in E minor", "same kit (808)").</param>
public sealed record LaunchpadSuggestion(string SampleName, IReadOnlyList<string> Reasons)
{
    ///<summary>
    ///The name followed by the reasons, for a menu: "Kick Vinyl Thud · same kit (vinyl)".
    ///</summary>
    public string Label => Reasons.Count == 0 ? SampleName : $"{SampleName} · {string.Join(", ", Reasons)}";
}

///<summary>
///A sound to put on a pad: which pad, and which sound of the Sound Bank.
///</summary>
///<param name="PadIndex">The pad, 0 to 63 (row * 8 + column, row 0 at the top).</param>
///<param name="SampleName">The sound's name in the Sound Bank, without the file extension.</param>
public sealed record LaunchpadPadAssignment(int PadIndex, string SampleName);
