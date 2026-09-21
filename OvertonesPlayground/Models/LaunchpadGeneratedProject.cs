namespace OvertonesPlayground.Models;

///<summary>
///A project the generator made, with a title and a description of what is on each bank.
///</summary>
///<param name="Project">The project, ready to load.</param>
///<param name="Title">Title with the style, tempo and key: "House · 124 BPM · F minor".</param>
///<param name="Description">What is on each bank and in the sequencer.</param>
public sealed record LaunchpadGeneratedProject(LaunchpadProject Project, string Title, string Description);
