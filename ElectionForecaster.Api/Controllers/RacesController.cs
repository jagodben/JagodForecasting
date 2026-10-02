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

        Dictionary<string, DetailedForecast> byId;
        try
        {
            byId = (await _orchestrator.GenerateAllForecastsAsync(type)).ToDictionary(f => f.RaceId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Forecasts failed; serving every race's fundamentals baseline");
            byId = new Dictionary<string, DetailedForecast>();
        }

        // GenerateAllForecastsAsync drops any race whose forecast threw — fill those from the
        // model's own baseline rather than serving RaceService's placeholder numbers.
        var result = new List<Race>(races.Count);
        foreach (var race in races)
        {
            var forecast = byId.GetValueOrDefault(race.Id)
                           ?? await ForecastOverlay.BaselineOrNullAsync(_orchestrator, race.Id, _logger);
            result.Add(ForecastOverlay.WithForecast(race, forecast));
        }
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetRace(string id)
    {
        var race = await _raceService.GetRaceByIdAsync(id);
        if (race == null)
            return NotFound();

        var forecast = await ForecastOverlay.ResolveAsync(_orchestrator, race.Id, _logger);
        return Ok(ForecastOverlay.WithForecast(race, forecast));
    }

}
