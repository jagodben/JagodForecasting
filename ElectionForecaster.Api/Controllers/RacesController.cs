using ElectionForecaster.Core.Enums;
using ElectionForecaster.Core.Interfaces;
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
            return Ok(races.Select(r => WithBlendedForecast(r, byId.GetValueOrDefault(r.Id))));
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
            return Ok(WithBlendedForecast(race, forecast));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Blended-forecast overlay failed for {RaceId}; serving baseline", id);
            return Ok(race);
        }
    }

    /// <summary>
    /// Returns a copy of the race whose rating and candidate win probabilities reflect the blended
    /// model forecast (markets + polls + fundamentals + national environment) rather than the
    /// fundamentals-only baseline RaceService computes at startup, so the map, ratings, and tooltips
    /// all agree with the race pages. Falls back to the baseline when no forecast is cached yet, and
    /// never mutates the shared race instance.
    /// </summary>
    private static Race WithBlendedForecast(Race race, DetailedForecast? f)
    {
        if (f == null) return race;

        // The Republican holds the R-side; the challenger slot (a Democrat or a viable independent)
        // carries the forecast's Dem-side probability.
        var repId = race.Candidates.FirstOrDefault(c => c.Party == Party.Republican)?.Id;
        var demId = race.Candidates.FirstOrDefault(c => c.Id != repId)?.Id;

        var forecasts = race.Forecasts.Select(fc => new Forecast
        {
            CandidateId = fc.CandidateId,
            CandidateName = fc.CandidateName,
            WinProbability = fc.CandidateId == demId ? f.DemWinProbability
                           : fc.CandidateId == repId ? f.RepWinProbability
                           : fc.WinProbability,
            ProjectedVoteShare = fc.CandidateId == demId ? f.DemVoteShare
                               : fc.CandidateId == repId ? f.RepVoteShare
                               : fc.ProjectedVoteShare
        }).ToList();

        return new Race
        {
            Id = race.Id,
            StateId = race.StateId,
            Type = race.Type,
            DistrictNumber = race.DistrictNumber,
            Rating = RatingFromProbability(f.DemWinProbability),
            Candidates = race.Candidates,
            Forecasts = forecasts,
            IsSpecialElection = race.IsSpecialElection,
            Year = race.Year
        };
    }

    // Same thresholds the maps and RaceService use, so the rating agrees with the win probability.
    private static RaceRating RatingFromProbability(double demProb) => demProb switch
    {
        >= 0.90 => RaceRating.SolidDem,
        >= 0.70 => RaceRating.LikelyDem,
        >= 0.55 => RaceRating.LeanDem,
        > 0.50 => RaceRating.TiltDem,
        >= 0.45 => RaceRating.TiltRep,
        >= 0.30 => RaceRating.LeanRep,
        >= 0.10 => RaceRating.LikelyRep,
        _ => RaceRating.SolidRep
    };
}
