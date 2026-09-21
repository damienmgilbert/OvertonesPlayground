namespace OvertonesPlayground.Models;

///<summary>
///What to generate a Launchpad project from. Everything but the style is optional: whatever is left out, the generator chooses
///(a tempo in the style's range, a key the sound bank has pitched sounds for, a kit that suits the style). The same request with the
///same seed always gives the same project.
///</summary>
///<param name="StyleKey">The style (see <see cref="LaunchpadStyleInfo.Key"/>).</param>
///<param name="Seed">Seed for the generator's random choices.</param>
///<param name="Tempo">Tempo in BPM, or null to choose one.</param>
///<param name="Key">Key such as "E minor", or null to choose one.</param>
///<param name="Kit">Kit key such as "808", or null to choose one.</param>
///<param name="Energy">How busy the drums are, 0 (sparse) to 1 (busy).</param>
public sealed record LaunchpadGenerationRequest(string StyleKey, int Seed, int? Tempo = null, string? Key = null, string? Kit = null, double Energy = 0.5);
