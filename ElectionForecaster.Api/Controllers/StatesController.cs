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
    private readonly IRaceService _raceService;
    private readonly IDistrictService _districtService;
    private readonly IForecastingOrchestrator _orchestrator;
    private readonly ILogger<StatesController> _logger;

    public StatesController(
        IStateService stateService,
        IRaceService raceService,
        IDistrictService districtService,
        IForecastingOrchestrator orchestrator,
        ILogger<StatesController> logger)
    {
        _stateService = stateService;
        _raceService = raceService;
        _districtService = districtService;
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
        return Ok(await WithBlendedForecastsAsync(state));
    }

    [HttpGet("{id}/races")]
    public async Task<IActionResult> GetStateRaces(string id)
    {
        var state = await _stateService.GetStateByIdAsync(id);
        if (state == null)
            return NotFound();

        var races = (await _raceService.GetRacesByStateAsync(id)).ToList();
        return Ok(await OverlayRacesAsync(races));
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

    /// <summary>
    /// Returns a copy of the state whose races (and district grid ratings) carry the blended model
    /// forecast, so the state page agrees with the dashboard map and the race pages. The underlying
    /// State/Race/District objects are startup singletons shared across requests — never mutated.
    /// Races whose forecast fails fall back to the startup baseline individually.
    /// </summary>
    private async Task<State> WithBlendedForecastsAsync(State state)
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
            OverallRating = state.OverallRating,
            Races = races,
            Districts = districts
        };
    }

    private async Task<List<Race>> OverlayRacesAsync(IEnumerable<Race> races)
    {
        var result = new List<Race>();
        foreach (var race in races)
        {
            try
            {
                var forecast = await _orchestrator.GenerateForecastAsync(race.Id);
                result.Add(ForecastOverlay.WithBlendedForecast(race, forecast));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Blended-forecast overlay failed for {RaceId}; serving baseline", race.Id);
                result.Add(race);
            }
        }
        return result;
    }
}
