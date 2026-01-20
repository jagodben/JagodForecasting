using ElectionForecaster.Core.Enums;
using ElectionForecaster.Core.Models;

namespace ElectionForecaster.Core.Interfaces;

public interface IRaceService
{
    Task<IEnumerable<Race>> GetAllRacesAsync(RaceType? type = null);
    Task<IEnumerable<Race>> GetRacesByStateAsync(string stateId);
    Task<Race?> GetRaceByIdAsync(string raceId);
}
