using ElectionForecaster.Core.Models;

namespace ElectionForecaster.Core.Interfaces;

public interface IDistrictService
{
    Task<IEnumerable<District>> GetDistrictsByStateAsync(string stateId);
    Task<District?> GetDistrictByIdAsync(string districtId);
}
