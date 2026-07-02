using ElectionForecaster.Core.Enums;
using ElectionForecaster.Core.Models;
using ElectionForecaster.Infrastructure.DataSources.Interfaces;
using ElectionForecaster.Infrastructure.DataSources.PredictionMarkets;
using ElectionForecaster.Infrastructure.Forecasting;
using Microsoft.AspNetCore.Mvc;

namespace ElectionForecaster.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ForecastController : ControllerBase
{
    private readonly IForecastingOrchestrator _orchestrator;
    private readonly PolymarketClient _polymarketClient;
    private readonly IPollingSource _pollingSource;
    private readonly ILogger<ForecastController> _logger;

    public ForecastController(
        IForecastingOrchestrator orchestrator,
        PolymarketClient polymarketClient,
        IPollingSource pollingSource,
        ILogger<ForecastController> logger)
    {
        _orchestrator = orchestrator;
        _polymarketClient = polymarketClient;
        _pollingSource = pollingSource;
        _logger = logger;
    }

    /// <summary>
    /// Gets the individual polls and weighted polling average for a race.
    /// </summary>
    [HttpGet("{raceId}/polls")]
    [ProducesResponseType(typeof(RacePolls), StatusCodes.Status200OK)]
    public async Task<ActionResult<RacePolls>> GetRacePolls(string raceId, [FromQuery] int days = 120)
    {
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
                IsPartisan = p.Methodology?.StartsWith("Partisan") ?? false
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
    /// Gets the overall chamber control odds from Polymarket.
    /// </summary>
    [HttpGet("chamber/{chamberType}/market-odds")]
    [ProducesResponseType(typeof(ChamberMarketOdds), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChamberMarketOdds>> GetChamberMarketOdds(string chamberType)
    {
        var odds = await _polymarketClient.GetChamberOddsAsync(chamberType);
        if (odds == null)
        {
            return NotFound(new { message = $"No market odds available for {chamberType}" });
        }

        return Ok(new ChamberMarketOdds
        {
            Chamber = chamberType,
            DemOdds = odds.DemOdds,
            RepOdds = odds.RepOdds,
            Timestamp = odds.Timestamp,
            Source = odds.Source
        });
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

/// <summary>
/// Chamber control odds from prediction markets.
/// </summary>
public class ChamberMarketOdds
{
    public string Chamber { get; set; } = "";
    public double DemOdds { get; set; }
    public double RepOdds { get; set; }
    public DateTime Timestamp { get; set; }
    public string Source { get; set; } = "";
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
