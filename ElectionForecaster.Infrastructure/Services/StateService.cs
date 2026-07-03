using ElectionForecaster.Core.Enums;
using ElectionForecaster.Core.Interfaces;
using ElectionForecaster.Core.Models;
using ElectionForecaster.Infrastructure.Data;

namespace ElectionForecaster.Infrastructure.Services;

public class StateService : IStateService
{
    private readonly List<State> _states;

    // Cook PVI data for state-level ratings
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

    public StateService(IRaceService raceService)
    {
        _states = ElectionDataProvider.GetAllStates();

        // Replace races in each state with the updated forecasted races from RaceService
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

            UpdateStateRating(state);
        }
    }

    private void UpdateStateRating(State state)
    {
        if (!StatePVI.TryGetValue(state.Id.ToUpperInvariant(), out var pvi))
            return;

        // Add midterm bonus for Democrats (4 points)
        var adjustedPvi = pvi + 4.0;

        // Convert to rating
        state.OverallRating = adjustedPvi switch
        {
            >= 12 => RaceRating.SolidDem,
            >= 6 => RaceRating.LikelyDem,
            >= 2 => RaceRating.LeanDem,
            >= 0 => RaceRating.TiltDem,
            >= -2 => RaceRating.TiltRep,
            >= -6 => RaceRating.LeanRep,
            >= -12 => RaceRating.LikelyRep,
            _ => RaceRating.SolidRep
        };
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
