using ElectionForecaster.Core.Enums;
using ElectionForecaster.Core.Interfaces;
using ElectionForecaster.Core.Models;
using ElectionForecaster.Infrastructure.Data;
using ElectionForecaster.Infrastructure.Data.Entities;
using ElectionForecaster.Infrastructure.DataSources.Candidates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ElectionForecaster.Infrastructure.Services;

/// <summary>
/// Keeps every race's candidates current: the daily job scrapes each race's nominees from
/// Wikipedia, persists them as <see cref="NomineeOverrideEntity"/> rows, and applies them to the
/// live singleton race objects (RaceService's races and the per-state district copies), so the
/// site reflects candidate changes without a redeploy. On startup the stored rows are re-applied
/// so a restart doesn't fall back to the compile-time nominee data.
/// </summary>
public class CandidateRefreshService
{
    private readonly WikipediaCandidateClient _client;
    private readonly ForecastDbContext _db;
    private readonly IRaceService _raceService;
    private readonly IStateService _stateService;
    private readonly ILogger<CandidateRefreshService> _logger;

    public CandidateRefreshService(
        WikipediaCandidateClient client,
        ForecastDbContext db,
        IRaceService raceService,
        IStateService stateService,
        ILogger<CandidateRefreshService> logger)
    {
        _client = client;
        _db = db;
        _raceService = raceService;
        _stateService = stateService;
        _logger = logger;
    }

    /// <summary>
    /// Scrapes nominees for every race from Wikipedia, stores what resolved, and applies it to
    /// the live races. A side Wikipedia can't resolve (TBD) keeps its previous value — a stored
    /// override if one exists, else the compile-time data. Returns the number of candidate
    /// fields that actually changed on live races.
    /// </summary>
    public async Task<int> RefreshFromWikipediaAsync(CancellationToken cancellationToken = default)
    {
        var races = (await _raceService.GetAllRacesAsync()).ToList();
        _logger.LogInformation("Candidate refresh: checking {Count} races against Wikipedia", races.Count);

        var scraped = await _client.FetchAllAsync(races, cancellationToken);
        _logger.LogInformation("Candidate refresh: Wikipedia resolved nominees for {Count} races", scraped.Count);

        var now = DateTime.UtcNow;
        foreach (var (raceId, nominees) in scraped)
        {
            if (nominees.Dem is null && nominees.Rep is null) continue;

            var row = await _db.NomineeOverrides.FindAsync(new object[] { raceId }, cancellationToken);
            if (row is null)
            {
                row = new NomineeOverrideEntity { RaceId = raceId };
                _db.NomineeOverrides.Add(row);
            }

            // Update only the sides Wikipedia resolved: a temporarily-unparseable side must not
            // wipe a previously-good name.
            if (nominees.Dem is not null)
            {
                row.DemName = nominees.Dem.Name;
                row.DemIsIncumbent = nominees.Dem.IsIncumbent;
            }
            if (nominees.Rep is not null)
            {
                row.RepName = nominees.Rep.Name;
                row.RepIsIncumbent = nominees.Rep.IsIncumbent;
            }
            row.UpdatedAt = now;
        }
        await _db.SaveChangesAsync(cancellationToken);

        return await ApplyStoredOverridesAsync(cancellationToken);
    }

    /// <summary>
    /// Applies every stored nominee override to the live singleton race objects. Called on
    /// startup (so stored overrides survive restarts) and after each refresh. Returns how many
    /// candidate fields changed.
    /// </summary>
    public async Task<int> ApplyStoredOverridesAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _db.NomineeOverrides.AsNoTracking().ToListAsync(cancellationToken);
        if (rows.Count == 0) return 0;

        // Collect every live Race object per id: RaceService's list (also referenced by each
        // state's Races) plus the separate per-district HouseRace copies inside StateService.
        var targets = new Dictionary<string, List<Race>>();
        void Add(Race? race)
        {
            if (race is null) return;
            (targets.TryGetValue(race.Id, out var list) ? list : targets[race.Id] = new List<Race>()).Add(race);
        }
        foreach (var race in await _raceService.GetAllRacesAsync()) Add(race);
        foreach (var state in await _stateService.GetAllStatesAsync())
            foreach (var district in state.Districts) Add(district.HouseRace);

        int changes = 0;
        foreach (var row in rows)
        {
            if (!targets.TryGetValue(row.RaceId, out var raceObjects)) continue;
            foreach (var race in raceObjects)
                changes += ApplyToRace(race, row);
        }

        if (changes > 0)
            _logger.LogInformation("Candidate refresh: applied {Changes} candidate updates to live races", changes);
        return changes;
    }

    /// <summary>Applies one override row to one live race; returns how many fields changed.</summary>
    private int ApplyToRace(Race race, NomineeOverrideEntity row)
    {
        int changes = 0;
        var repCandidate = race.Candidates.FirstOrDefault(c => c.Party == Party.Republican);
        var demCandidate = race.Candidates.FirstOrDefault(c => c.Id != repCandidate?.Id);

        // The challenger slot may hold an editorially-designated independent (e.g. Dan Osborn) —
        // the scraped Democratic nominee must not displace them.
        var independentChallenger = IndependentChallengers.Get(race.Id) is { ReplacesDem: true };

        if (row.DemName is not null && demCandidate is not null && !independentChallenger)
            changes += ApplyToCandidate(race, demCandidate, row.DemName, row.DemIsIncumbent);
        if (row.RepName is not null && repCandidate is not null)
            changes += ApplyToCandidate(race, repCandidate, row.RepName, row.RepIsIncumbent);
        return changes;
    }

    private int ApplyToCandidate(Race race, Candidate candidate, string name, bool isIncumbent)
    {
        // Same name → keep the current (curated) incumbency flag; the scrape's incumbency
        // inference (nominee == the infobox's officeholder) is only trusted when it comes with an
        // actual candidate change, so a badly-formatted infobox can't clear a correct flag.
        if (candidate.Name.Equals(name, StringComparison.Ordinal)) return 0;

        _logger.LogInformation("{RaceId}: candidate '{Old}' → '{New}'{Inc}",
            race.Id, candidate.Name, name, isIncumbent ? " (incumbent)" : "");
        candidate.Name = name;
        candidate.IsIncumbent = isIncumbent;
        var forecast = race.Forecasts.FirstOrDefault(f => f.CandidateId == candidate.Id);
        if (forecast is not null) forecast.CandidateName = name;
        return 1;
    }
}
