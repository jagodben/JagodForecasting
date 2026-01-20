using ElectionForecaster.Core.Models;

namespace ElectionForecaster.Core.Interfaces;

public interface IStateService
{
    Task<IEnumerable<State>> GetAllStatesAsync();
    Task<State?> GetStateByIdAsync(string stateId);
}
