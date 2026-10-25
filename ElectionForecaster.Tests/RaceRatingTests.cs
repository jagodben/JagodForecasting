using ElectionForecaster.Api.Services;
using ElectionForecaster.Core.Enums;

namespace ElectionForecaster.Tests;

/// <summary>
/// Pins the margin bands behind race ratings (Tilt under 1, Lean 1–4, Likely 4–8, Solid 8+), rated
/// on the margin as displayed so the rating never disagrees with the "D+x" beside it.
/// </summary>
public class RaceRatingTests
{
    [Theory]
    [InlineData(0.3, RaceRating.TiltDem)]
    [InlineData(0.94, RaceRating.TiltDem)]   // shows as D+0.9
    [InlineData(0.96, RaceRating.LeanDem)]   // shows as D+1
    [InlineData(1.0, RaceRating.LeanDem)]
    [InlineData(3.9, RaceRating.LeanDem)]
    [InlineData(4.0, RaceRating.LikelyDem)]
    [InlineData(7.9, RaceRating.LikelyDem)]
    [InlineData(8.0, RaceRating.SolidDem)]
    [InlineData(25, RaceRating.SolidDem)]
    [InlineData(-0.5, RaceRating.TiltRep)]
    [InlineData(-1.0, RaceRating.LeanRep)]
    [InlineData(-4.0, RaceRating.LikelyRep)]
    [InlineData(-8.0, RaceRating.SolidRep)]
    [InlineData(-30, RaceRating.SolidRep)]
    public void Bands(double margin, RaceRating expected) =>
        Assert.Equal(expected, ForecastOverlay.RatingFromMargin(margin));

    [Theory]
    [InlineData(0.04, RaceRating.TiltDem)]   // rounds to 0, shows as D+0.1
    [InlineData(-0.04, RaceRating.TiltRep)]  // rounds to 0, shows as R+0.1
    [InlineData(0.0, RaceRating.TiltDem)]
    public void NearZeroGoesToTheTrueSide(double margin, RaceRating expected) =>
        Assert.Equal(expected, ForecastOverlay.RatingFromMargin(margin));

    [Fact]
    public void HalfwayRoundsUpLikeTheClient()
    {
        // JS Math.round(-9.5) is -9, so the client shows -0.95 as R+0.9; .NET's default banker's
        // rounding would make it R+1 and rate it Lean.
        Assert.Equal(RaceRating.TiltRep, ForecastOverlay.RatingFromMargin(-0.95));
    }
}
