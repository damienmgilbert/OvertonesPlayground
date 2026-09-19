namespace OvertonesPlayground.Models;

///<summary>
///A progress report from an audio import.
///</summary>
///<param name="Stage">The step the import is working through.</param>
///<param name="Fraction">How far through the whole import it is, from 0 to 1 (not just through <paramref name="Stage"/>).</param>
public readonly record struct ImportProgress(ImportStage Stage, double Fraction);
