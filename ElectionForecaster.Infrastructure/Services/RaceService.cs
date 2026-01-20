using ElectionForecaster.Core.Enums;
using ElectionForecaster.Core.Interfaces;
using ElectionForecaster.Core.Models;
using ElectionForecaster.Infrastructure.Data;

namespace ElectionForecaster.Infrastructure.Services;

public class RaceService : IRaceService
{
    private readonly List<Race> _races;

    public RaceService()
    {
        var states = MockDataProvider.GetAllStates();
        _races = states.SelectMany(s => s.Races).ToList();
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
