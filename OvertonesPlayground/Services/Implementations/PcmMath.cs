namespace OvertonesPlayground.Services.Implementations;

///<summary>
///Shared arithmetic for 16-bit PCM sample processing, used by every service that mixes, gains, or otherwise
///recomputes sample values.
///</summary>
internal static class PcmMath
{
    #region Public methods
    ///<summary>
    ///Rounds and clamps a sample value into the valid 16-bit PCM range.
    ///</summary>
    public static short ClampToShort(double value) => (short)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), short.MinValue, short.MaxValue);
    #endregion
}
