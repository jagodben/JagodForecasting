using ElectionForecaster.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ElectionForecaster.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DistrictsController : ControllerBase
{
    private readonly IDistrictService _districtService;

    public DistrictsController(IDistrictService districtService)
    {
        _districtService = districtService;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetDistrict(string id)
    {
        var district = await _districtService.GetDistrictByIdAsync(id);
        if (district == null)
            return NotFound();
        return Ok(district);
    }
}
