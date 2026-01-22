using ElectionForecaster.Core.Enums;
using ElectionForecaster.Core.Models;
using ElectionForecaster.Infrastructure.Forecasting;
using Microsoft.AspNetCore.Mvc;

namespace ElectionForecaster.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ForecastController : ControllerBase
{
    private readonly IForecastingOrchestrator _orchestrator;
    private readonly ILogger<ForecastController> _logger;

    public ForecastController(
        IForecastingOrchestrator orchestrator,
        ILogger<ForecastController> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    /// <summary>
    /// Gets the detailed forecast for a specific race.
    /// </summary>
    [HttpGet("{raceId}")]
    [ProducesResponseType(typeof(DetailedForecast), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DetailedForecast>> GetRaceForecast(string raceId)
    {
        try
        {
            var forecast = await _orchestrator.GenerateForecastAsync(raceId);
            return Ok(forecast);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating forecast for {RaceId}", raceId);
            return NotFound(new { message = $"Could not generate forecast for race {raceId}" });
        }
    }

    /// <summary>
    /// Gets forecasts for all races of a specific type.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<DetailedForecast>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<DetailedForecast>>> GetAllForecasts([FromQuery] RaceType? type = null)
    {
        var forecasts = await _orchestrator.GenerateAllForecastsAsync(type);
        return Ok(forecasts);
    }

    /// <summary>
    /// Gets the chamber control forecast (Senate or House).
    /// </summary>
    [HttpGet("chamber/{chamberType}")]
    [ProducesResponseType(typeof(ChamberForecast), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ChamberForecast>> GetChamberForecast(string chamberType)
    {
        if (!Enum.TryParse<RaceType>(chamberType, true, out var chamber) ||
            (chamber != RaceType.Senate && chamber != RaceType.House))
        {
            return BadRequest(new { message = "Chamber type must be 'Senate' or 'House'" });
        }

        var forecast = await _orchestrator.SimulateChamberAsync(chamber);
        return Ok(forecast);
    }

    /// <summary>
    /// Triggers a refresh of all data sources (admin endpoint).
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RefreshData()
    {
        _logger.LogInformation("Manual data refresh triggered");
        await _orchestrator.RefreshAllDataAsync();
        return Ok(new { message = "Data refresh completed" });
    }

    /// <summary>
    /// Triggers storage of daily snapshot (admin endpoint).
    /// </summary>
    [HttpPost("snapshot")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> StoreDailySnapshot()
    {
        _logger.LogInformation("Manual snapshot storage triggered");
        await _orchestrator.StoreDailySnapshotAsync();
        return Ok(new { message = "Daily snapshot stored" });
    }

    /// <summary>
    /// Gets a summary of all forecasts grouped by race type.
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ForecastSummary), StatusCodes.Status200OK)]
    public async Task<ActionResult<ForecastSummary>> GetForecastSummary()
    {
        var senateForecasts = await _orchestrator.GenerateAllForecastsAsync(RaceType.Senate);
        var houseForecasts = await _orchestrator.GenerateAllForecastsAsync(RaceType.House);
        var governorForecasts = await _orchestrator.GenerateAllForecastsAsync(RaceType.Governor);

        var senateChamber = await _orchestrator.SimulateChamberAsync(RaceType.Senate);
        var houseChamber = await _orchestrator.SimulateChamberAsync(RaceType.House);

        var summary = new ForecastSummary
        {
            LastUpdated = DateTime.UtcNow,
            Senate = new RaceTypeSummary
            {
                TotalRaces = senateForecasts.Count,
                DemFavored = senateForecasts.Count(f => f.DemWinProbability > 0.5),
                RepFavored = senateForecasts.Count(f => f.RepWinProbability > 0.5),
                Tossups = senateForecasts.Count(f => Math.Abs(f.DemWinProbability - 0.5) < 0.1),
                ChamberForecast = senateChamber
            },
            House = new RaceTypeSummary
            {
                TotalRaces = houseForecasts.Count,
                DemFavored = houseForecasts.Count(f => f.DemWinProbability > 0.5),
                RepFavored = houseForecasts.Count(f => f.RepWinProbability > 0.5),
                Tossups = houseForecasts.Count(f => Math.Abs(f.DemWinProbability - 0.5) < 0.1),
                ChamberForecast = houseChamber
            },
            Governor = new RaceTypeSummary
            {
                TotalRaces = governorForecasts.Count,
                DemFavored = governorForecasts.Count(f => f.DemWinProbability > 0.5),
                RepFavored = governorForecasts.Count(f => f.RepWinProbability > 0.5),
                Tossups = governorForecasts.Count(f => Math.Abs(f.DemWinProbability - 0.5) < 0.1),
                ChamberForecast = null
            }
        };

        return Ok(summary);
    }
}

/// <summary>
/// Summary of all forecasts.
/// </summary>
public class ForecastSummary
{
    public DateTime LastUpdated { get; set; }
    public RaceTypeSummary Senate { get; set; } = new();
    public RaceTypeSummary House { get; set; } = new();
    public RaceTypeSummary Governor { get; set; } = new();
}

/// <summary>
/// Summary for a specific race type.
/// </summary>
public class RaceTypeSummary
{
    public int TotalRaces { get; set; }
    public int DemFavored { get; set; }
    public int RepFavored { get; set; }
    public int Tossups { get; set; }
    public ChamberForecast? ChamberForecast { get; set; }
}
