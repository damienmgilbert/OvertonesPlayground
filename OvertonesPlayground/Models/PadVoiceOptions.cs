namespace OvertonesPlayground.Models;

///<summary>
///How one Launchpad voice should sound when it is started.
///</summary>
///<remarks>
///Always create it with at least one argument, for example <c>new PadVoiceOptions(Volume: 1)</c>. A bare
///<c>new PadVoiceOptions()</c> is the struct default, not the parameter defaults below: volume 0 and speed 0, which is silent
///and, because speed 0 counts as a speed change, makes Android's player fail with MEDIA_ERROR_UNSUPPORTED.
///</remarks>
///<param name="Volume">Volume, from 0 to 1.</param>
///<param name="Balance">Stereo pan, from -1 (left) to 1 (right).</param>
///<param name="Speed">Playback speed, where 1 is normal; also changes pitch. Ignored where the player can't change speed.</param>
///<param name="Loop">Whether the sample repeats until stopped.</param>
///<param name="MaxLength">If set, the voice is cut off after this long.</param>
public readonly record struct PadVoiceOptions(double Volume = 1, double Balance = 0, double Speed = 1, bool Loop = false, TimeSpan? MaxLength = null);
