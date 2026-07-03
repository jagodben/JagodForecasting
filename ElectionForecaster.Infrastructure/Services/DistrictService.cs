using ElectionForecaster.Core.Interfaces;
using ElectionForecaster.Core.Models;
using ElectionForecaster.Infrastructure.Data;

namespace ElectionForecaster.Infrastructure.Services;

public class DistrictService : IDistrictService
{
    private readonly List<District> _districts;

    public DistrictService()
    {
        var states = ElectionDataProvider.GetAllStates();
        _districts = states.SelectMany(s => s.Districts).ToList();
    }

    public Task<IEnumerable<District>> GetDistrictsByStateAsync(string stateId)
    {
        var districts = _districts.Where(d => d.StateId.Equals(stateId, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(districts);
    }

    public Task<District?> GetDistrictByIdAsync(string districtId)
    {
        var district = _districts.FirstOrDefault(d => d.Id.Equals(districtId, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(district);
    }
}
