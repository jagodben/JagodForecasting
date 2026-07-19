using ElectionForecaster.Core.Enums;
using ElectionForecaster.Core.Interfaces;
using ElectionForecaster.Core.Models;
using ElectionForecaster.Infrastructure.Data;
using ElectionForecaster.Infrastructure.DataSources.Interfaces;
using ElectionForecaster.Infrastructure.Forecasting;
using Microsoft.AspNetCore.Mvc;

namespace ElectionForecaster.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ForecastController : ControllerBase
{
    private readonly IForecastingOrchestrator _orchestrator;
    private readonly IPollingSource _pollingSource;
    private readonly IRaceService _raceService;
    private readonly ILogger<ForecastController> _logger;

    public ForecastController(
        IForecastingOrchestrator orchestrator,
        IPollingSource pollingSource,
        IRaceService raceService,
        ILogger<ForecastController> logger)
    {
        _orchestrator = orchestrator;
        _pollingSource = pollingSource;
        _raceService = raceService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the individual polls and weighted polling average for a race.
    /// </summary>
    [HttpGet("{raceId}/polls")]
    [ProducesResponseType(typeof(RacePolls), StatusCodes.Status200OK)]
    public async Task<ActionResult<RacePolls>> GetRacePolls(string raceId, [FromQuery] int days = 120)
    {
        // Unknown race ids must 404, not trigger Wikipedia fetches for pages that can't exist.
        if (await _raceService.GetRaceByIdAsync(raceId) is null)
            return NotFound(new { message = $"Unknown race '{raceId}'" });

        // Races with a viable independent challenger have no usable polling: the parsed Dem-vs-Rep
        // tables poll the token Democrat (not the independent), so the forecast drops them. Return
        // none here too, rather than mislabelling the Democrat's numbers as the independent's.
        if (IndependentChallengers.Has(raceId))
            return Ok(new RacePolls { RaceId = raceId, Average = null, Polls = new List<PollDto>() });

        var polls = await _pollingSource.GetRecentPollsAsync(raceId, days);
        var average = await _pollingSource.GetPollingAverageAsync(raceId);

        return Ok(new RacePolls
        {
            RaceId = raceId,
            Average = average,
            Polls = polls.Select(p => new PollDto
            {
                Pollster = p.Pollster,
                Date = p.Date,
                SampleSize = p.SampleSize,
                Population = p.Population,
                DemPercent = p.DemPercent,
                RepPercent = p.RepPercent,
                Margin = p.Margin,
                IsPartisan = p.IsPartisan
            }).ToList()
        });
    }

    /// <summary>
    /// Gets the detailed forecast for a specific race.
    /// </summary>
    [HttpGet("{raceId}")]
    [ProducesResponseType(typeof(DetailedForecast), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DetailedForecast>> GetRaceForecast(string raceId)
    {
        // Unknown race ids must 404 — without this the orchestrator fabricates a forecast
        // (defaulting the race type) for any string, and caches/persists the junk.
        if (await _raceService.GetRaceByIdAsync(raceId) is null)
            return NotFound(new { message = $"Unknown race '{raceId}'" });

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
    /// Gets the chamber control-over-time history (cheap DB read; Senate has a backfilled series).
    /// </summary>
    [HttpGet("chamber/{chamberType}/history")]
    [ProducesResponseType(typeof(List<ChamberHistoryPoint>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ChamberHistoryPoint>>> GetChamberHistory(string chamberType)
    {
        var history = await _orchestrator.GetChamberHistoryAsync(chamberType);
        return Ok(history);
    }

    /// <summary>
    /// Guards the admin POST endpoints. In Development they're open; in Production they require
    /// the X-Admin-Key header to match the ADMIN_KEY environment variable, and are disabled
    /// entirely when no key is configured. Keeps public visitors (the routes are visible in the
    /// public repo) from triggering expensive refreshes/rebuilds.
    /// </summary>
    private IActionResult? RequireAdmin()
    {
        var env = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        if (env.IsDevelopment()) return null;

        var configuredKey = Environment.GetEnvironmentVariable("ADMIN_KEY");
        if (string.IsNullOrEmpty(configuredKey)) return NotFound();

        return Request.Headers["X-Admin-Key"] == configuredKey ? null : Unauthorized();
    }

    /// <summary>
    /// Triggers a refresh of all data sources (admin endpoint).
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RefreshData()
    {
        if (RequireAdmin() is { } denied) return denied;
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
        if (RequireAdmin() is { } denied) return denied;
        _logger.LogInformation("Manual snapshot storage triggered");
        await _orchestrator.StoreDailySnapshotAsync();
        return Ok(new { message = "Daily snapshot stored" });
    }

    /// <summary>
    /// Fills any gaps in the retrospective model forecast history (from June 1). Admin endpoint.
    /// Recorded days are never replaced — this only reconstructs dates that have no row.
    /// </summary>
    [HttpPost("backfill")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> BackfillModelHistory()
    {
        if (RequireAdmin() is { } denied) return denied;
        _logger.LogInformation("Manual model history backfill triggered (gap fill)");
        await _orchestrator.BackfillModelHistoryAsync(force: true);
        return Ok(new { message = "Model history backfill completed" });
    }

}

/// <summary>
/// Individual polls plus the weighted average for a race.
/// </summary>
public class RacePolls
{
    public string RaceId { get; set; } = "";
    public ElectionForecaster.Infrastructure.DataSources.Models.PollingAverage? Average { get; set; }
    public List<PollDto> Polls { get; set; } = new();
}

/// <summary>
/// A single poll for API responses.
/// </summary>
public class PollDto
{
    public string Pollster { get; set; } = "";
    public DateTime Date { get; set; }
    public int? SampleSize { get; set; }
    public string? Population { get; set; }
    public double DemPercent { get; set; }
    public double RepPercent { get; set; }
    public double Margin { get; set; }
    public bool IsPartisan { get; set; }
}
