using ElectionForecaster.Core.Interfaces;
using ElectionForecaster.Core.Models;
using ElectionForecaster.Infrastructure.Data;

namespace ElectionForecaster.Infrastructure.Services;

public class StateService : IStateService
{
    private readonly List<State> _states;

    public StateService()
    {
        _states = MockDataProvider.GetAllStates();
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
