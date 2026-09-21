namespace OvertonesPlayground.Models;

///<summary>
///A drum kit of the Sound Bank.
///</summary>
///<param name="Key">Kit key, for example <c>808</c>.</param>
///<param name="Name">Name to show, for example "Roland TR-808".</param>
///<param name="SampleCount">How many sounds of the Sound Bank belong to it.</param>
public sealed record LaunchpadKitInfo(string Key, string Name, int SampleCount);
