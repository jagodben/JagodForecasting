using ElectionForecaster.Core.Enums;
using ElectionForecaster.Core.Interfaces;
using ElectionForecaster.Api.Services;
using ElectionForecaster.Core.Models;
using ElectionForecaster.Infrastructure.Forecasting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ElectionForecaster.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RacesController : ControllerBase
{
    private readonly IRaceService _raceService;
    private readonly IForecastingOrchestrator _orchestrator;
    private readonly ILogger<RacesController> _logger;

    public RacesController(
        IRaceService raceService,
        IForecastingOrchestrator orchestrator,
        ILogger<RacesController> logger)
    {
        _raceService = raceService;
        _orchestrator = orchestrator;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllRaces([FromQuery] RaceType? type = null)
    {
        var races = (await _raceService.GetAllRacesAsync(type)).ToList();
        try
        {
            var byId = (await _orchestrator.GenerateAllForecastsAsync(type))
                .ToDictionary(f => f.RaceId);
            return Ok(races.Select(r => ForecastOverlay.WithBlendedForecast(r, byId.GetValueOrDefault(r.Id))));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Blended-forecast overlay failed; serving baseline ratings");
            return Ok(races);
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetRace(string id)
    {
        var race = await _raceService.GetRaceByIdAsync(id);
        if (race == null)
            return NotFound();

        try
        {
            var forecast = await _orchestrator.GenerateForecastAsync(id);
            return Ok(ForecastOverlay.WithBlendedForecast(race, forecast));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Blended-forecast overlay failed for {RaceId}; serving baseline", id);
            return Ok(race);
        }
    }

}
