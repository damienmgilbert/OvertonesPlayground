namespace OvertonesPlayground.Models;

///<summary>
///A style the project generator can make projects in.
///</summary>
///<param name="Key">Stable identifier, for example <c>boom-bap</c>.</param>
///<param name="Name">Name to show, for example "Boom Bap".</param>
///<param name="Summary">One line about how the style sounds.</param>
///<param name="MinTempo">Slowest tempo of the style, in BPM.</param>
///<param name="MaxTempo">Fastest tempo of the style, in BPM.</param>
public sealed record LaunchpadStyleInfo(string Key, string Name, string Summary, int MinTempo, int MaxTempo);
