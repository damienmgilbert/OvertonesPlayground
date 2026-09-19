using OvertonesPlayground.Services.Implementations;

namespace OvertonesPlayground.Tests.Services;

public sealed class PcmMathTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1234, 1234)]
    [InlineData(-1234, -1234)]
    [InlineData(32767, 32767)]
    [InlineData(-32768, -32768)]
    public void ClampToShort_ValueInRange_IsUnchanged(double input, short expected)
    {
        Assert.Equal(expected, PcmMath.ClampToShort(input));
    }

    [Theory]
    [InlineData(0.4, 0)]
    [InlineData(0.6, 1)]
    [InlineData(1234.49, 1234)]
    [InlineData(1234.51, 1235)]
    [InlineData(-0.4, 0)]
    [InlineData(-0.6, -1)]
    [InlineData(-1234.49, -1234)]
    [InlineData(-1234.51, -1235)]
    [InlineData(0.9999999999, 1)]
    public void ClampToShort_FractionalValue_RoundsToTheNearestSample(double input, short expected)
    {
        Assert.Equal(expected, PcmMath.ClampToShort(input));
    }

    [Theory]
    [InlineData(0.5, 1)]
    [InlineData(1.5, 2)]
    [InlineData(2.5, 3)]
    [InlineData(-0.5, -1)]
    [InlineData(-1.5, -2)]
    [InlineData(-2.5, -3)]
    public void ClampToShort_ExactlyHalfway_RoundsAwayFromZeroSoPositiveAndNegativeMatch(double input, short expected)
    {
        Assert.Equal(expected, PcmMath.ClampToShort(input));
    }

    [Theory]
    [InlineData(32766.6, 32767)]
    [InlineData(32767.4, 32767)]
    [InlineData(-32767.6, -32768)]
    [InlineData(-32768.4, -32768)]
    public void ClampToShort_NearTheEdges_RoundsThenStaysInRange(double input, short expected)
    {
        Assert.Equal(expected, PcmMath.ClampToShort(input));
    }

    [Fact]
    public void ClampToShort_NaN_IsSilence()
    {
        Assert.Equal(0, PcmMath.ClampToShort(double.NaN));
    }

    [Theory]
    [InlineData(32768)]
    [InlineData(100000)]
    [InlineData(double.PositiveInfinity)]
    public void ClampToShort_ValueAboveRange_ClampsToMaxValue(double input)
    {
        Assert.Equal(short.MaxValue, PcmMath.ClampToShort(input));
    }

    [Theory]
    [InlineData(-32769)]
    [InlineData(-100000)]
    [InlineData(double.NegativeInfinity)]
    public void ClampToShort_ValueBelowRange_ClampsToMinValue(double input)
    {
        Assert.Equal(short.MinValue, PcmMath.ClampToShort(input));
    }
}
