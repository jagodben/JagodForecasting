using ElectionForecaster.Api.Services;
using ElectionForecaster.Core.Interfaces;
using ElectionForecaster.Core.Models;
using ElectionForecaster.Infrastructure.Forecasting;
using Microsoft.AspNetCore.Mvc;

namespace ElectionForecaster.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StatesController : ControllerBase
{
    private readonly IStateService _stateService;
    private readonly IForecastingOrchestrator _orchestrator;
    private readonly ILogger<StatesController> _logger;

    public StatesController(
        IStateService stateService,
        IForecastingOrchestrator orchestrator,
        ILogger<StatesController> logger)
    {
        _stateService = stateService;
        _orchestrator = orchestrator;
        _logger = logger;
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
        return Ok(await WithForecastsAsync(state));
    }

    /// <summary>
    /// Returns a copy of the state whose races (and district grid ratings) carry the model's
    /// forecast, so the state page agrees with the dashboard map and the race pages. The underlying
    /// State/Race/District objects are startup singletons shared across requests — never mutated.
    /// A race whose forecast fails falls back to the model's own fundamentals baseline.
    /// </summary>
    private async Task<State> WithForecastsAsync(State state)
    {
        var races = await OverlayRacesAsync(state.Races);
        var byId = races.ToDictionary(r => r.Id);

        var districts = state.Districts.Select(d =>
        {
            var houseRace = d.HouseRace != null ? byId.GetValueOrDefault(d.HouseRace.Id, d.HouseRace) : null;
            return new District
            {
                Id = d.Id,
                StateId = d.StateId,
                Number = d.Number,
                Rating = houseRace?.Rating ?? d.Rating,
                HouseRace = houseRace
            };
        }).ToList();

        return new State
        {
            Id = state.Id,
            Name = state.Name,
            ElectoralVotes = state.ElectoralVotes,
            CongressionalDistricts = state.CongressionalDistricts,
            Races = races,
            Districts = districts
        };
    }

    private async Task<List<Race>> OverlayRacesAsync(IEnumerable<Race> races)
    {
        var result = new List<Race>();
        foreach (var race in races)
        {
            var forecast = await ForecastOverlay.ResolveAsync(_orchestrator, race.Id, _logger);
            result.Add(ForecastOverlay.WithForecast(race, forecast));
        }
        return result;
    }
}
