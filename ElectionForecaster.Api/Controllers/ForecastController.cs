using ElectionForecaster.Core.Enums;
using ElectionForecaster.Core.Interfaces;
using ElectionForecaster.Core.Models;
using ElectionForecaster.Infrastructure.Data;
using ElectionForecaster.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
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
    private readonly ForecastDbContext _dbContext;
    private readonly ILogger<ForecastController> _logger;

    public ForecastController(
        IForecastingOrchestrator orchestrator,
        IPollingSource pollingSource,
        IRaceService raceService,
        ForecastDbContext dbContext,
        ILogger<ForecastController> logger)
    {
        _orchestrator = orchestrator;
        _pollingSource = pollingSource;
        _raceService = raceService;
        _dbContext = dbContext;
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
                IsPartisan = p.IsPartisan,
                PartisanLean = p.PartisanLean
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

    /// <summary>
    /// Every stored poll across all races, newest first — the data behind the Polls page.
    /// Served straight from the DB (no scraping); rows accumulate as the daily refresh and
    /// race-page visits fetch new polls from Wikipedia.
    /// </summary>
    [HttpGet("polls")]
    [ProducesResponseType(typeof(List<SitePollDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SitePollDto>>> GetAllPolls()
    {
        // Page cutoff: nothing conducted before May 2026.
        var cutoff = new DateTime(2026, 5, 1);
        var rows = await _dbContext.Polls.AsNoTracking()
            .Where(p => p.Date >= cutoff)
            .OrderByDescending(p => p.Date)
            .ToListAsync();

        return Ok(rows.Select(p => new SitePollDto
        {
            RaceId = p.RaceId,
            Pollster = p.Pollster,
            Date = p.Date,
            SampleSize = p.SampleSize,
            Population = p.Population,
            DemPercent = p.DemPercent,
            RepPercent = p.RepPercent,
            Margin = p.DemPercent - p.RepPercent,
            IsPartisan = p.Methodology != null && p.Methodology.StartsWith("Partisan"),
            PartisanLean = Infrastructure.DataSources.Models.PollData.PartisanLeanOf(p.Methodology)
        }).ToList());
    }

    /// <summary>
    /// Full dump of the persisted model state (history, chambers, ballot series, polls,
    /// overrides, settings) for the nightly offsite backup. Everything here is public data
    /// already served by other endpoints, just in one restorable document.
    /// </summary>
    [HttpGet("export")]
    [ProducesResponseType(typeof(ForecastExport), StatusCodes.Status200OK)]
    public async Task<ActionResult<ForecastExport>> ExportHistory()
    {
        var export = new ForecastExport
        {
            ExportedAt = DateTime.UtcNow,
            ForecastHistory = await _dbContext.ForecastHistory.AsNoTracking().OrderBy(f => f.RaceId).ThenBy(f => f.Date).ToListAsync(),
            ChamberHistory = await _dbContext.ChamberHistory.AsNoTracking().OrderBy(c => c.Chamber).ThenBy(c => c.Date).ToListAsync(),
            GenericBallot = await _dbContext.GenericBallot.AsNoTracking().OrderBy(g => g.Date).ToListAsync(),
            Polls = await _dbContext.Polls.AsNoTracking().OrderBy(p => p.RaceId).ThenBy(p => p.Date).ToListAsync(),
            NomineeOverrides = await _dbContext.NomineeOverrides.AsNoTracking().ToListAsync(),
            Settings = await _dbContext.Settings.AsNoTracking().ToListAsync()
        };
        return Ok(export);
    }

    /// <summary>
    /// Break-glass restore from a backup produced by GET export. Obeys the immutability rule:
    /// only rows whose key doesn't exist are inserted — recorded days are never replaced, so
    /// importing an old backup after a disk loss recovers history without touching anything
    /// written since. Admin endpoint (requires ADMIN_KEY in production).
    /// </summary>
    [HttpPost("import")]
    [RequestSizeLimit(200_000_000)]
    public async Task<IActionResult> ImportHistory()
    {
        if (RequireAdmin() is { } denied) return denied;

        // Deserialized by hand: the dump is our own database's contents, and a single row that
        // predates a validation rule must not make the whole restore impossible.
        var backup = await System.Text.Json.JsonSerializer.DeserializeAsync<ForecastExport>(
            Request.Body, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        if (backup == null) return BadRequest(new { message = "Empty or unreadable backup body" });

        var haveFh = (await _dbContext.ForecastHistory.Select(f => new { f.RaceId, f.Date }).ToListAsync())
            .Select(x => (x.RaceId, x.Date)).ToHashSet();
        var haveCh = (await _dbContext.ChamberHistory.Select(c => new { c.Chamber, c.Date }).ToListAsync())
            .Select(x => (x.Chamber, x.Date)).ToHashSet();
        var haveBallot = (await _dbContext.GenericBallot.Select(g => g.Date).ToListAsync()).ToHashSet();
        var havePolls = (await _dbContext.Polls.Select(p => new { p.RaceId, p.Pollster, p.Date }).ToListAsync())
            .Select(x => (x.RaceId, x.Pollster, x.Date)).ToHashSet();
        var haveOverrides = (await _dbContext.NomineeOverrides.Select(n => n.RaceId).ToListAsync()).ToHashSet();
        var haveSettings = (await _dbContext.Settings.Select(k => k.Key).ToListAsync()).ToHashSet();

        int fh = 0, ch = 0, gb = 0, po = 0, no = 0, se = 0;
        foreach (var r in backup.ForecastHistory.Where(r => !haveFh.Contains((r.RaceId, r.Date))))
        {
            r.Id = 0; _dbContext.ForecastHistory.Add(r); fh++;
        }
        foreach (var r in backup.ChamberHistory.Where(r => !haveCh.Contains((r.Chamber, r.Date))))
        {
            r.Id = 0; _dbContext.ChamberHistory.Add(r); ch++;
        }
        foreach (var r in backup.GenericBallot.Where(r => !haveBallot.Contains(r.Date)))
        {
            r.Id = 0; _dbContext.GenericBallot.Add(r); gb++;
        }
        foreach (var r in backup.Polls.Where(r => !havePolls.Contains((r.RaceId, r.Pollster, r.Date))))
        {
            r.Id = 0; _dbContext.Polls.Add(r); po++;
        }
        foreach (var r in backup.NomineeOverrides.Where(r => !haveOverrides.Contains(r.RaceId)))
        {
            _dbContext.NomineeOverrides.Add(r); no++;
        }
        foreach (var r in backup.Settings.Where(r => !haveSettings.Contains(r.Key)))
        {
            _dbContext.Settings.Add(r); se++;
        }

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Backup import: +{Fh} history, +{Ch} chamber, +{Gb} ballot, +{Po} polls, +{No} overrides, +{Se} settings",
            fh, ch, gb, po, no, se);
        return Ok(new { forecastHistory = fh, chamberHistory = ch, genericBallot = gb, polls = po, nomineeOverrides = no, settings = se });
    }
}

/// <summary>Restorable dump of every persisted table (see GET/POST export/import).</summary>
public class ForecastExport
{
    public DateTime ExportedAt { get; set; }
    public List<ForecastHistoryEntity> ForecastHistory { get; set; } = new();
    public List<ChamberHistoryEntity> ChamberHistory { get; set; } = new();
    public List<GenericBallotEntity> GenericBallot { get; set; } = new();
    public List<PollEntity> Polls { get; set; } = new();
    public List<NomineeOverrideEntity> NomineeOverrides { get; set; } = new();
    public List<SettingEntity> Settings { get; set; } = new();
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
    public string? PartisanLean { get; set; }
}

/// <summary>A poll row on the all-polls page: PollDto plus which race it belongs to.</summary>
public class SitePollDto : PollDto
{
    public string RaceId { get; set; } = "";
}
