using ElectionForecaster.Core.Enums;
using ElectionForecaster.Core.Interfaces;
using ElectionForecaster.Core.Models;
using ElectionForecaster.Infrastructure.Data;

namespace ElectionForecaster.Infrastructure.Services;

public class StateService : IStateService
{
    private readonly List<State> _states;

    public StateService(IRaceService raceService)
    {
        _states = ElectionDataProvider.GetAllStates();

        // Point each state at RaceService's shared race instances, so candidate refreshes applied
        // to those instances show up here too. (Neither service forecasts — the API overlays it.)
        var allRaces = raceService.GetAllRacesAsync().Result.ToList();
        foreach (var state in _states)
        {
            var stateRaces = allRaces.Where(r => r.StateId.Equals(state.Id, StringComparison.OrdinalIgnoreCase)).ToList();
            state.Races = stateRaces;

            // Also update district house races
            foreach (var district in state.Districts)
            {
                district.HouseRace = stateRaces.FirstOrDefault(r =>
                    r.Type == RaceType.House && r.DistrictNumber == district.Number);
                if (district.HouseRace != null)
                {
                    district.Rating = district.HouseRace.Rating;
                }
            }
        }
    }

    public Task<IEnumerable<State>> GetAllStatesAsync()
    {
        return Task.FromResult<IEnumerable<State>>(_states);
    }

    public Task<State?> GetStateByIdAsync(string stateId)
    {
        var state = _states.FirstOrDefault(s => s.Id.Equals(stateId, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(state);
    }
}
