using OvertonesPlayground.Services.Implementations;

namespace OvertonesPlayground.Tests.Services;

public sealed class FastFourierTransformTests
{
    private const int Size = 64;
    private const double Tolerance = 1e-9;

    [Fact]
    public void Forward_Impulse_ProducesFlatSpectrum()
    {
        double[] real = new double[Size];
        double[] imaginary = new double[Size];
        real[0] = 1;

        FastFourierTransform.Forward(real, imaginary);

        for (int bin = 0; bin < Size; bin++)
        {
            Assert.Equal(1, real[bin], Tolerance);
            Assert.Equal(0, imaginary[bin], Tolerance);
        }
    }

    [Fact]
    public void Forward_ConstantSignal_PutsAllEnergyInBinZero()
    {
        double[] real = Enumerable.Repeat(0.5, Size).ToArray();
        double[] imaginary = new double[Size];

        FastFourierTransform.Forward(real, imaginary);

        Assert.Equal(0.5 * Size, real[0], Tolerance);
        for (int bin = 1; bin < Size; bin++)
        {
            Assert.Equal(0, double.Hypot(real[bin], imaginary[bin]), Tolerance);
        }
    }

    [Fact]
    public void Forward_SineAtBinFive_PutsEnergyAtBinFiveAndItsMirror()
    {
        const int targetBin = 5;
        double[] real = new double[Size];
        double[] imaginary = new double[Size];
        for (int i = 0; i < Size; i++)
        {
            real[i] = Math.Sin(2 * Math.PI * targetBin * i / Size);
        }

        FastFourierTransform.Forward(real, imaginary);

        // A real sine of amplitude 1 splits its energy evenly between +bin and -bin, each with magnitude N/2.
        Assert.Equal(Size / 2.0, double.Hypot(real[targetBin], imaginary[targetBin]), Tolerance);
        Assert.Equal(Size / 2.0, double.Hypot(real[Size - targetBin], imaginary[Size - targetBin]), Tolerance);

        for (int bin = 0; bin < Size; bin++)
        {
            bool isTarget = bin == targetBin || bin == Size - targetBin;
            if (!isTarget)
            {
                Assert.Equal(0, double.Hypot(real[bin], imaginary[bin]), 1e-8);
            }
        }
    }

    [Fact]
    public void Inverse_AfterForward_RestoresTheOriginalSignal()
    {
        Random random = new(1234);
        double[] original = new double[Size];
        for (int i = 0; i < Size; i++)
        {
            original[i] = (random.NextDouble() * 2) - 1;
        }

        double[] real = (double[])original.Clone();
        double[] imaginary = new double[Size];

        FastFourierTransform.Forward(real, imaginary);
        FastFourierTransform.Inverse(real, imaginary);

        for (int i = 0; i < Size; i++)
        {
            Assert.Equal(original[i], real[i], Tolerance);
            Assert.Equal(0, imaginary[i], Tolerance);
        }
    }

    [Fact]
    public void Forward_SingleSample_LeavesTheSampleUnchanged()
    {
        double[] real = [0.75];
        double[] imaginary = [0];

        FastFourierTransform.Forward(real, imaginary);

        Assert.Equal(0.75, real[0]);
        Assert.Equal(0, imaginary[0]);
    }
}
