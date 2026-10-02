using ElectionForecaster.Core.Enums;
using ElectionForecaster.Core.Interfaces;
using ElectionForecaster.Core.Models;
using ElectionForecaster.Infrastructure.Data;

namespace ElectionForecaster.Infrastructure.Services;

/// <summary>
/// The registry of 2026 races and their candidates. It deliberately computes no forecast: every
/// probability, margin and rating comes from <c>ForecastingOrchestrator</c>, which the API
/// overlays onto these objects. A second model here once ran its own fundamentals (a fixed
/// midterm bonus, its own incumbency term, no prior results or independent challengers) and
/// leaked through every fallback path, quietly disagreeing with the forecast it stood in for.
/// </summary>
public class RaceService : IRaceService
{
    private readonly List<Race> _races;

    public RaceService()
    {
        var states = ElectionDataProvider.GetAllStates();
        _races = states.SelectMany(s => s.Races).ToList();

        foreach (var race in _races)
        {
            InferPlaceholderIncumbency(race);
        }
    }

    /// <summary>
    /// Real per-candidate incumbency comes from the scraped nominee data, which correctly reflects
    /// open seats (a retired incumbent's party keeps no incumbent). A House district still showing
    /// both placeholders has no nominee data at all, so its incumbent party is inferred from the
    /// 2024 winner — the orchestrator reads incumbency straight off these candidates.
    /// </summary>
    private static void InferPlaceholderIncumbency(Race race)
    {
        if (race.Type != RaceType.House || !race.DistrictNumber.HasValue) return;

        var result2024 = DistrictElectionData.GetResult2024(race.StateId, race.DistrictNumber.Value);
        if (!result2024.HasValue) return;

        var demCandidate = race.Candidates.FirstOrDefault(c => c.Party == Party.Democrat);
        var repCandidate = race.Candidates.FirstOrDefault(c => c.Party == Party.Republican);
        bool unresolved = demCandidate?.Name == ElectionDataProvider.DemPlaceholder &&
                          repCandidate?.Name == ElectionDataProvider.RepPlaceholder;
        if (!unresolved) return;

        var republicanIncumbent = result2024.Value.RepublicanWon;
        if (demCandidate != null) demCandidate.IsIncumbent = !republicanIncumbent;
        if (repCandidate != null) repCandidate.IsIncumbent = republicanIncumbent;
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
