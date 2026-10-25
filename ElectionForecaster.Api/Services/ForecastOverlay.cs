using ElectionForecaster.Core.Enums;
using ElectionForecaster.Core.Models;
using ElectionForecaster.Infrastructure.Forecasting;

namespace ElectionForecaster.Api.Services;

/// <summary>
/// Overlays the model's forecast onto the static race objects RaceService holds, so every endpoint
/// serves the numbers the model produces. RaceService computes no forecast of its own — its races
/// carry only seed placeholders — so a race must never be served without an overlay. Returns
/// copies: the underlying Race/State/District instances are long-lived singletons shared across
/// requests and must never be mutated.
/// </summary>
public static class ForecastOverlay
{
    /// <summary>
    /// The forecast to serve for a race: its full forecast, or — if that fails — the model's own
    /// fundamentals-only baseline, so a failure degrades to fewer inputs rather than to a different
    /// model's numbers. Null only when both fail.
    /// </summary>
    public static async Task<DetailedForecast?> ResolveAsync(IForecastingOrchestrator orchestrator, string raceId, ILogger logger)
    {
        try
        {
            return await orchestrator.GenerateForecastAsync(raceId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Forecast failed for {RaceId}; serving its fundamentals baseline", raceId);
        }
        return await BaselineOrNullAsync(orchestrator, raceId, logger);
    }

    /// <summary>The race's fundamentals-only baseline, or null (logged) if even that fails.</summary>
    public static async Task<DetailedForecast?> BaselineOrNullAsync(IForecastingOrchestrator orchestrator, string raceId, ILogger logger)
    {
        try
        {
            return await orchestrator.GenerateBaselineForecastAsync(raceId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fundamentals baseline also failed for {RaceId}; serving it unforecast", raceId);
            return null;
        }
    }

    /// <summary>
    /// Returns a copy of the race whose rating and candidate win probabilities reflect the given
    /// forecast. Falls back to the race as-is only when no forecast is available at all.
    /// </summary>
    public static Race WithForecast(Race race, DetailedForecast? f)
    {
        if (f == null) return race;

        // The Republican holds the R-side; the challenger slot (a Democrat or a viable independent)
        // carries the forecast's Dem-side probability.
        var repId = race.Candidates.FirstOrDefault(c => c.Party == Party.Republican)?.Id;
        var demId = race.Candidates.FirstOrDefault(c => c.Id != repId)?.Id;

        var forecasts = race.Forecasts.Select(fc => new Forecast
        {
            CandidateId = fc.CandidateId,
            CandidateName = fc.CandidateName,
            WinProbability = fc.CandidateId == demId ? f.DemWinProbability
                           : fc.CandidateId == repId ? f.RepWinProbability
                           : fc.WinProbability,
            ProjectedVoteShare = fc.CandidateId == demId ? f.DemVoteShare
                               : fc.CandidateId == repId ? f.RepVoteShare
                               : fc.ProjectedVoteShare
        }).ToList();

        return new Race
        {
            Id = race.Id,
            StateId = race.StateId,
            Type = race.Type,
            DistrictNumber = race.DistrictNumber,
            Rating = RatingFromMargin(f.ExpectedDemMargin),
            ProjectedDemMargin = f.ExpectedDemMargin,
            Candidates = race.Candidates,
            Forecasts = forecasts,
            IsSpecialElection = race.IsSpecialElection,
            Year = race.Year
        };
    }

    /// <summary>
    /// A race's rating from its projected Dem margin in points: Tilt under 1, Lean 1–4, Likely 4–8,
    /// Solid 8+. Rated on the margin as the site displays it — rounded to 0.1 half-up like the
    /// client's Math.round, a rounded zero going to the true side — so a race shown as "D+1" is
    /// Lean, never Tilt. The client's ratingFromMargin (utils/ratings.ts) mirrors this.
    /// </summary>
    public static RaceRating RatingFromMargin(double demMargin)
    {
        var rounded = Math.Floor(demMargin * 10 + 0.5) / 10;
        var demLeads = rounded != 0 ? rounded > 0 : demMargin >= 0;
        return Math.Abs(rounded) switch
        {
            < 1 => demLeads ? RaceRating.TiltDem : RaceRating.TiltRep,
            < 4 => demLeads ? RaceRating.LeanDem : RaceRating.LeanRep,
            < 8 => demLeads ? RaceRating.LikelyDem : RaceRating.LikelyRep,
            _ => demLeads ? RaceRating.SolidDem : RaceRating.SolidRep
        };
    }
}
