namespace OvertonesPlayground.Models;

///<summary>
///A ready-made Launchpad setup that can be loaded from the Projects menu: what it is called and what is in it.
///</summary>
///<param name="Id">Stable identifier, for example <c>boom-bap</c>.</param>
///<param name="Name">Title shown in the list, with the tempo and key: "Boom Bap · 90 BPM · E minor".</param>
///<param name="Description">What is on each bank and how to play it, shown when the setup is loaded.</param>
public sealed record LaunchpadExampleInfo(string Id, string Name, string Description);
