using ElectionForecaster.Core.Enums;
using ElectionForecaster.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ElectionForecaster.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RacesController : ControllerBase
{
    private readonly IRaceService _raceService;

    public RacesController(IRaceService raceService)
    {
        _raceService = raceService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllRaces([FromQuery] RaceType? type = null)
    {
        var races = await _raceService.GetAllRacesAsync(type);
        return Ok(races);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetRace(string id)
    {
        var race = await _raceService.GetRaceByIdAsync(id);
        if (race == null)
            return NotFound();
        return Ok(race);
    }
}
