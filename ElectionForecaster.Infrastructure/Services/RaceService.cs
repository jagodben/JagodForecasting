using ElectionForecaster.Core.Enums;
using ElectionForecaster.Core.Interfaces;
using ElectionForecaster.Core.Models;
using ElectionForecaster.Infrastructure.Data;

namespace ElectionForecaster.Infrastructure.Services;

public class RaceService : IRaceService
{
    private readonly List<Race> _races;

    // Cook PVI data for fundamentals-based forecasting
    private static readonly Dictionary<string, double> StatePVI = new()
    {
        { "AL", -15 }, { "AK", -9 }, { "AZ", -2 }, { "AR", -16 },
        { "CA", 14 }, { "CO", 6 }, { "CT", 8 }, { "DE", 7 },
        { "FL", -6 }, { "GA", 0 }, { "HI", 15 }, { "ID", -19 },
        { "IL", 8 }, { "IN", -10 }, { "IA", -6 }, { "KS", -10 },
        { "KY", -16 }, { "LA", -13 }, { "ME", 3 }, { "MD", 14 },
        { "MA", 16 }, { "MI", 1 }, { "MN", 2 }, { "MS", -10 },
        { "MO", -10 }, { "MT", -11 }, { "NE", -12 }, { "NV", 0 },
        { "NH", 1 }, { "NJ", 7 }, { "NM", 5 }, { "NY", 10 },
        { "NC", -3 }, { "ND", -20 }, { "OH", -6 }, { "OK", -20 },
        { "OR", 6 }, { "PA", 0 }, { "RI", 10 }, { "SC", -8 },
        { "SD", -16 }, { "TN", -14 }, { "TX", -5 }, { "UT", -11 },
        { "VT", 16 }, { "VA", 4 }, { "WA", 8 }, { "WV", -23 },
        { "WI", 0 }, { "WY", -25 }
    };

    // Midterm penalty for president's party (assumed Republican president 2025-2029)
    private const double MidtermPenalty = 4.0; // Points shift toward Dems
    private const double IncumbencyAdvantage = 3.5;

    public RaceService()
    {
        var states = ElectionDataProvider.GetAllStates();
        _races = states.SelectMany(s => s.Races).ToList();

        // Apply real forecasts based on fundamentals
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

        // For House races, use real district-level data
        if (race.Type == RaceType.House && race.DistrictNumber.HasValue)
        {
            // Get real district PVI (positive = Republican lean)
            pvi = -DistrictElectionData.GetDistrictPVI(race.StateId, race.DistrictNumber.Value);

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
            // For Senate/Governor, use state PVI
            pvi = StatePVI.TryGetValue(race.StateId.ToUpperInvariant(), out var statePvi) ? statePvi : 0;
        }

        var demCandidate = race.Candidates.FirstOrDefault(c => c.Party == Party.Democrat);
        var repCandidate = race.Candidates.FirstOrDefault(c => c.Party == Party.Republican);

        // Real per-candidate incumbency now comes from the scraped nominee data, which correctly
        // reflects open seats (a retired incumbent's party keeps no incumbent). Only fall back to the
        // 2024-winner heuristic for districts we couldn't resolve — still showing the placeholders.
        bool unresolved = demCandidate?.Name == "Democratic Nominee" && repCandidate?.Name == "Republican Nominee";
        if (race.Type == RaceType.House && priorMargin.HasValue && unresolved)
        {
            if (demCandidate != null) demCandidate.IsIncumbent = !republicanIncumbent;
            if (repCandidate != null) repCandidate.IsIncumbent = republicanIncumbent;
        }

        // Calculate expected margin for Democrats using weighted inputs
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
            // No prior data, use fundamentals only
            demMargin = pvi + MidtermPenalty;
        }

        // Add incumbency advantage
        if (demCandidate?.IsIncumbent == true)
            demMargin += IncumbencyAdvantage;
        else if (repCandidate?.IsIncumbent == true)
            demMargin -= IncumbencyAdvantage;

        // Convert margin to probability using normal CDF approximation
        // Standard error varies by race type
        double standardError = race.Type switch
        {
            RaceType.Senate => 6.0,
            RaceType.Governor => 6.5,
            RaceType.House => 8.0,
            _ => 7.0
        };

        double demProb = NormalCdf(demMargin / standardError);
        double repProb = 1.0 - demProb;

        // Ensure reasonable bounds
        demProb = Math.Max(0.02, Math.Min(0.98, demProb));
        repProb = Math.Max(0.02, Math.Min(0.98, repProb));

        // Calculate vote shares
        double demVoteShare = 0.50 + (demMargin / 100.0);
        demVoteShare = Math.Max(0.30, Math.Min(0.70, demVoteShare));
        double repVoteShare = 1.0 - demVoteShare;

        // Update forecasts
        foreach (var forecast in race.Forecasts)
        {
            var candidate = race.Candidates.FirstOrDefault(c => c.Id == forecast.CandidateId);
            if (candidate?.Party == Party.Democrat)
            {
                forecast.WinProbability = demProb;
                forecast.ProjectedVoteShare = demVoteShare;
            }
            else if (candidate?.Party == Party.Republican)
            {
                forecast.WinProbability = repProb;
                forecast.ProjectedVoteShare = repVoteShare;
            }
        }

        // Update race rating based on probability
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

    private static double NormalCdf(double x)
    {
        const double a1 = 0.254829592;
        const double a2 = -0.284496736;
        const double a3 = 1.421413741;
        const double a4 = -1.453152027;
        const double a5 = 1.061405429;
        const double p = 0.3275911;

        int sign = x < 0 ? -1 : 1;
        x = Math.Abs(x) / Math.Sqrt(2);

        double t = 1.0 / (1.0 + p * x);
        double y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);

        return 0.5 * (1.0 + sign * y);
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
