using ElectionForecaster.Infrastructure.Forecasting;

namespace ElectionForecaster.Tests;

public class ForecastMathTests
{
    [Fact]
    public void TCdf_IsAValidCdf()
    {
        Assert.Equal(0.5, ForecastMath.TCdf(0), 10);
        Assert.True(ForecastMath.TCdf(-100) < 1e-6);
        Assert.True(ForecastMath.TCdf(100) > 1 - 1e-6);

        // Strictly increasing
        double prev = -1;
        for (var x = -6.0; x <= 6.0; x += 0.25)
        {
            var p = ForecastMath.TCdf(x);
            Assert.True(p > prev);
            prev = p;
        }
    }

    [Fact]
    public void TInverse_RoundTripsWithTCdf()
    {
        foreach (var p in new[] { 0.01, 0.1, 0.25, 0.5, 0.75, 0.9, 0.99 })
        {
            var x = ForecastMath.TInverse(p);
            Assert.Equal(p, ForecastMath.TCdf(x), 6);
        }
    }

    [Fact]
    public void MarginToProbability_TiedRaceIsCoinFlip()
    {
        Assert.Equal(0.5, ForecastMath.MarginToProbability(0, 6), 10);
    }

    [Fact]
    public void MarginToProbability_IsSymmetric()
    {
        var dem = ForecastMath.MarginToProbability(4.5, 6);
        var rep = ForecastMath.MarginToProbability(-4.5, 6);
        Assert.Equal(1.0, dem + rep, 10);
    }

    [Fact]
    public void MarginToProbability_GrowsWithMarginAndShrinksWithUncertainty()
    {
        Assert.True(ForecastMath.MarginToProbability(6, 6) > ForecastMath.MarginToProbability(3, 6));
        // The same lead is less of a lock when the race is noisier
        Assert.True(ForecastMath.MarginToProbability(6, 10) < ForecastMath.MarginToProbability(6, 5));
    }

    [Fact]
    public void MarginToProbability_FatTailsKeepBlowoutsShortOfCertainty()
    {
        // A D+20 at SE 6 is overwhelming but the t(5) tails must keep it below 100%
        var p = ForecastMath.MarginToProbability(20, 6);
        Assert.True(p > 0.98);
        Assert.True(p < 1.0);
    }
}
