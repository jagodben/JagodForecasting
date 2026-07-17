using ElectionForecaster.Core.Enums;
using ElectionForecaster.Core.Interfaces;
using ElectionForecaster.Core.Models;
using ElectionForecaster.Infrastructure.Data;
using ElectionForecaster.Infrastructure.Forecasting;

namespace ElectionForecaster.Infrastructure.Services;

public class RaceService : IRaceService
{
    private readonly List<Race> _races;

    // Midterm penalty for president's party (assumed Republican president 2025-2029)
    private const double MidtermPenalty = 4.0; // Points shift toward Dems
    private const double IncumbencyAdvantage = 3.5;

    public RaceService()
    {
        var states = ElectionDataProvider.GetAllStates();
        _races = states.SelectMany(s => s.Races).ToList();

        foreach (var race in _races)
        {
            ApplyFundamentalsBasedForecast(race);
        }
    }

    private void ApplyFundamentalsBasedForecast(Race race)
    {
        double pvi;
        double? priorMargin = null;
        bool republicanIncumbent = false;

        if (race.Type == RaceType.House && race.DistrictNumber.HasValue)
        {
            // District partisan lean index as a Dem lean (falls back to state lean for unlisted districts).
            pvi = PartisanLean.GetDistrictLean(race.StateId, race.DistrictNumber.Value);

            // Get 2024 results (positive margin = Republican won)
            var result2024 = DistrictElectionData.GetResult2024(race.StateId, race.DistrictNumber.Value);
            if (result2024.HasValue)
            {
                // Convert to Dem margin (negative of Rep margin)
                priorMargin = -result2024.Value.Margin;
                republicanIncumbent = result2024.Value.RepublicanWon;
            }
        }
        else
        {
            // For Senate/Governor, use state PVI.
            pvi = PartisanLean.GetStateLean(race.StateId);
        }

        var demCandidate = race.Candidates.FirstOrDefault(c => c.Party == Party.Democrat);
        var repCandidate = race.Candidates.FirstOrDefault(c => c.Party == Party.Republican);

        // Real per-candidate incumbency now comes from the scraped nominee data, which correctly
        // reflects open seats (a retired incumbent's party keeps no incumbent). Only fall back to the
        // 2024-winner heuristic for districts we couldn't resolve — still showing the placeholders.
        bool unresolved = demCandidate?.Name == ElectionDataProvider.DemPlaceholder &&
                          repCandidate?.Name == ElectionDataProvider.RepPlaceholder;
        if (race.Type == RaceType.House && priorMargin.HasValue && unresolved)
        {
            if (demCandidate != null) demCandidate.IsIncumbent = !republicanIncumbent;
            if (repCandidate != null) repCandidate.IsIncumbent = republicanIncumbent;
        }

        double demMargin;

        if (priorMargin.HasValue)
        {
            // Weight: 40% prior results, 40% PVI fundamentals, 20% midterm environment
            double fundamentalsMargin = pvi + MidtermPenalty;
            double priorAdjusted = priorMargin.Value + MidtermPenalty; // Adjust prior for midterm swing
            demMargin = (priorAdjusted * 0.4) + (fundamentalsMargin * 0.4) + (MidtermPenalty * 0.2);
        }
        else
        {
            demMargin = pvi + MidtermPenalty;
        }

        if (demCandidate?.IsIncumbent == true)
            demMargin += IncumbencyAdvantage;
        else if (repCandidate?.IsIncumbent == true)
            demMargin -= IncumbencyAdvantage;

        double standardError = race.Type switch
        {
            RaceType.Senate => 6.0,
            RaceType.Governor => 6.5,
            RaceType.House => 8.0,
            _ => 7.0
        };

        double demProb = ForecastMath.MarginToProbability(demMargin, standardError);
        double repProb = 1.0 - demProb;

        demProb = Math.Max(0.02, Math.Min(0.98, demProb));
        repProb = Math.Max(0.02, Math.Min(0.98, repProb));

        double demVoteShare = 0.50 + (demMargin / 100.0);
        demVoteShare = Math.Max(0.30, Math.Min(0.70, demVoteShare));
        double repVoteShare = 1.0 - demVoteShare;

        // Update forecasts. The Republican gets the R-side; the challenger slot — a Democrat or a
        // viable independent — gets the D-side, so an independent challenger flows through unchanged.
        var repId = race.Candidates.FirstOrDefault(c => c.Party == Party.Republican)?.Id;
        foreach (var forecast in race.Forecasts)
        {
            if (forecast.CandidateId == repId)
            {
                forecast.WinProbability = repProb;
                forecast.ProjectedVoteShare = repVoteShare;
            }
            else
            {
                forecast.WinProbability = demProb;
                forecast.ProjectedVoteShare = demVoteShare;
            }
        }

        race.Rating = demProb switch
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

    public Task<IEnumerable<Race>> GetAllRacesAsync(RaceType? type = null)
    {
        IEnumerable<Race> races = _races;
        if (type.HasValue)
        {
            races = races.Where(r => r.Type == type.Value);
        }
        return Task.FromResult(races);
    }

    public Task<IEnumerable<Race>> GetRacesByStateAsync(string stateId)
    {
        var races = _races.Where(r => r.StateId.Equals(stateId, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(races);
    }

    public Task<Race?> GetRaceByIdAsync(string raceId)
    {
        var race = _races.FirstOrDefault(r => r.Id.Equals(raceId, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(race);
    }
}
