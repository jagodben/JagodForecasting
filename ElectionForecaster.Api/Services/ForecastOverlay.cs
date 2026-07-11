using ElectionForecaster.Core.Enums;
using ElectionForecaster.Core.Models;

namespace ElectionForecaster.Api.Services;

/// <summary>
/// Overlays the blended model forecast (markets + polls + fundamentals + national environment)
/// onto the static race objects RaceService builds at startup, so every endpoint serves the same
/// numbers the model produces. Returns copies — the underlying Race/State/District instances are
/// long-lived singletons shared across requests and must never be mutated.
/// </summary>
public static class ForecastOverlay
{
    /// <summary>
    /// Returns a copy of the race whose rating and candidate win probabilities reflect the blended
    /// forecast rather than the fundamentals-only startup baseline. Falls back to the race as-is
    /// when no forecast is available.
    /// </summary>
    public static Race WithBlendedForecast(Race race, DetailedForecast? f)
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
            Rating = RatingFromProbability(f.DemWinProbability),
            Candidates = race.Candidates,
            Forecasts = forecasts,
            IsSpecialElection = race.IsSpecialElection,
            Year = race.Year
        };
    }

    // Same thresholds the maps and RaceService use, so the rating agrees with the win probability.
    public static RaceRating RatingFromProbability(double demProb) => demProb switch
    {
        >= 0.90 => RaceRating.SolidDem,
        >= 0.70 => RaceRating.LikelyDem,
        >= 0.55 => RaceRating.LeanDem,
        > 0.50 => RaceRating.TiltDem,
        >= 0.45 => RaceRating.TiltRep,
        >= 0.30 => RaceRating.LeanRep,
        >= 0.10 => RaceRating.LikelyRep,
        _ => RaceRating.SolidRep
    };
}
