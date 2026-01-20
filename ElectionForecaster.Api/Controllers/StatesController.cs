using ElectionForecaster.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ElectionForecaster.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StatesController : ControllerBase
{
    private readonly IStateService _stateService;
    private readonly IRaceService _raceService;
    private readonly IDistrictService _districtService;

    public StatesController(IStateService stateService, IRaceService raceService, IDistrictService districtService)
    {
        _stateService = stateService;
        _raceService = raceService;
        _districtService = districtService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllStates()
    {
        var states = await _stateService.GetAllStatesAsync();
        var summaries = states.Select(s => new
        {
            s.Id,
            s.Name,
            s.ElectoralVotes,
            s.CongressionalDistricts,
            s.OverallRating,
            RaceCount = s.Races.Count
        });
        return Ok(summaries);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetState(string id)
    {
        var state = await _stateService.GetStateByIdAsync(id);
        if (state == null)
            return NotFound();
        return Ok(state);
    }

    [HttpGet("{id}/races")]
    public async Task<IActionResult> GetStateRaces(string id)
    {
        var state = await _stateService.GetStateByIdAsync(id);
        if (state == null)
            return NotFound();

        var races = await _raceService.GetRacesByStateAsync(id);
        return Ok(races);
    }

    [HttpGet("{id}/districts")]
    public async Task<IActionResult> GetStateDistricts(string id)
    {
        var state = await _stateService.GetStateByIdAsync(id);
        if (state == null)
            return NotFound();

        var districts = await _districtService.GetDistrictsByStateAsync(id);
        return Ok(districts);
    }
}
