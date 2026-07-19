using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using ElectionForecaster.Infrastructure.Data;
using ElectionForecaster.Infrastructure.Data.Entities;
using ElectionForecaster.Infrastructure.DataSources.Interfaces;
using ElectionForecaster.Infrastructure.DataSources.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ElectionForecaster.Infrastructure.DataSources.Polling;

/// <summary>
/// Fetches general-election polling by parsing the poll tables on the corresponding English
/// Wikipedia race article via the MediaWiki API. Covers statewide races (Senate/Governor), the
/// at-large House states, and multi-district House races (whose polls live in a "District N"
/// section of the state-wide House article — only the few polled districts have one).
/// </summary>
public partial class WikipediaPollingClient : IPollingSource
{
    private readonly HttpClient _httpClient;
    private readonly ForecastDbContext _dbContext;
    private readonly ILogger<WikipediaPollingClient> _logger;

    // Per-race cache of parsed polls with the time they were fetched.
    private readonly Dictionary<string, (DateTime FetchedAt, List<PollData> Polls)> _cache = new();
    // Cross-race pollster house effects, estimated from every persisted poll (see GetHouseEffectsAsync).
    private (DateTime FetchedAt, Dictionary<string, double> Effects)? _houseEffects;
    private readonly SemaphoreSlim _fetchLock = new(1, 1);
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(6);
    // A race that resolved to zero polls is cached only briefly, so a transient empty/failed fetch
    // (rate-limit, maxlag, a temporarily-broken page) is retried soon instead of being stuck for 6h.
    private static readonly TimeSpan EmptyCacheTtl = TimeSpan.FromMinutes(20);

    private const string ApiBase = "https://en.wikipedia.org/w/api.php";

    public string SourceName => "Wikipedia";

    public WikipediaPollingClient(
        HttpClient httpClient,
        ForecastDbContext dbContext,
        ILogger<WikipediaPollingClient> logger)
    {
        _httpClient = httpClient;
        _dbContext = dbContext;
        _logger = logger;

        // Wikipedia's API requires a descriptive User-Agent or it returns 403.
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "ElectionForecaster/1.0 (https://jagodforecasting.com; contact via site)");
        }
    }

    public async Task<PollingAverage?> GetPollingAverageAsync(string raceId, CancellationToken cancellationToken = default)
    {
        var polls = await GetRecentPollsAsync(raceId, 90, cancellationToken);
        if (polls.Count == 0) return null;
        var houseEffects = await GetHouseEffectsAsync(cancellationToken);
        return PollingAverageCalculator.Calculate(polls, raceId, houseEffects: houseEffects);
    }

    public async Task<List<PollData>> GetRecentPollsAsync(string raceId, int days = 30, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-days);
        var polls = await GetRacePollsAsync(raceId, cancellationToken);
        return polls
            .Where(p => p.Date >= cutoff)
            .OrderByDescending(p => p.Date)
            .ToList();
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        // Invalidate the in-memory cache so the next access re-fetches from Wikipedia.
        // We intentionally don't prefetch all ~70 statewide races here to stay polite to
        // the API; each race is fetched lazily (and persisted) on first access after refresh.
        lock (_cache)
        {
            _cache.Clear();
            _houseEffects = null;
        }
        await Task.CompletedTask;
    }

    /// <summary>
    /// Estimates pollster house effects from every poll we've persisted (all races), so a
    /// pollster's lean is measured across the whole map rather than the single race being averaged.
    /// Cached for <see cref="CacheTtl"/>; the DB read is guarded by the fetch lock because the
    /// scoped <see cref="ForecastDbContext"/> isn't safe for concurrent use.
    /// </summary>
    private async Task<Dictionary<string, double>> GetHouseEffectsAsync(CancellationToken cancellationToken)
    {
        lock (_cache)
        {
            if (_houseEffects is { } cached && DateTime.UtcNow - cached.FetchedAt < CacheTtl)
                return cached.Effects;
        }

        await _fetchLock.WaitAsync(cancellationToken);
        try
        {
            lock (_cache)
            {
                if (_houseEffects is { } cached && DateTime.UtcNow - cached.FetchedAt < CacheTtl)
                    return cached.Effects;
            }

            var allPolls = await _dbContext.Polls
                .AsNoTracking()
                .Select(e => new PollData
                {
                    RaceId = e.RaceId,
                    Pollster = e.Pollster,
                    DemPercent = e.DemPercent,
                    RepPercent = e.RepPercent,
                    Methodology = e.Methodology
                })
                .ToListAsync(cancellationToken);

            var effects = PollsterHouseEffects.Estimate(allPolls);
            lock (_cache) _houseEffects = (DateTime.UtcNow, effects);

            if (effects.Count > 0)
                _logger.LogInformation("Estimated house effects for {Count} pollsters from {Polls} polls",
                    effects.Count, allPolls.Count);
            return effects;
        }
        finally
        {
            _fetchLock.Release();
        }
    }

    /// <summary>
    /// Returns all parsed polls for a race, using the in-memory cache, then the database,
    /// then a fresh fetch from Wikipedia.
    /// </summary>
    private async Task<List<PollData>> GetRacePollsAsync(string raceId, CancellationToken cancellationToken)
    {
        // Only statewide races have parseable Wikipedia poll tables.
        if (GetPageTitle(raceId) is null)
            return new List<PollData>();

        if (TryGetCached(raceId, out var cachedPolls))
            return cachedPolls;

        await _fetchLock.WaitAsync(cancellationToken);
        try
        {
            // Re-check the cache after acquiring the lock (another caller may have filled it).
            if (TryGetCached(raceId, out cachedPolls))
                return cachedPolls;

            List<PollData> polls;
            try
            {
                polls = await FetchAndParseAsync(raceId, cancellationToken);
                if (polls.Count > 0)
                    await SavePollsToDbAsync(polls, cancellationToken);
                else
                {
                    // A parse that yields no polls (broken page, or an error body that slipped past
                    // the parse-property check) must not zero out a race that has polls in the DB.
                    // Fall back to what we've previously persisted.
                    var dbPolls = await LoadPollsFromDbAsync(raceId, cancellationToken);
                    if (dbPolls.Count > 0)
                    {
                        _logger.LogInformation("Wikipedia returned no polls for {RaceId}; using {Count} from the DB", raceId, dbPolls.Count);
                        polls = dbPolls;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Wikipedia poll fetch failed for {RaceId}; falling back to DB", raceId);
                polls = await LoadPollsFromDbAsync(raceId, cancellationToken);
            }

            lock (_cache) _cache[raceId] = (DateTime.UtcNow, polls);
            return polls;
        }
        finally
        {
            _fetchLock.Release();
        }
    }

    /// <summary>
    /// Returns cached polls if the entry is still fresh. Empty entries expire on the short
    /// <see cref="EmptyCacheTtl"/> so a transient miss is retried soon; non-empty ones on the full TTL.
    /// </summary>
    private bool TryGetCached(string raceId, out List<PollData> polls)
    {
        lock (_cache)
        {
            if (_cache.TryGetValue(raceId, out var entry))
            {
                var ttl = entry.Polls.Count > 0 ? CacheTtl : EmptyCacheTtl;
                if (DateTime.UtcNow - entry.FetchedAt < ttl)
                {
                    polls = entry.Polls;
                    return true;
                }
            }
        }
        polls = new List<PollData>();
        return false;
    }

    private async Task<List<PollData>> FetchAndParseAsync(string raceId, CancellationToken cancellationToken)
    {
        var title = GetPageTitle(raceId)!;
        var url = $"{ApiBase}?action=parse&prop=wikitext&formatversion=2&redirects=1&format=json&page={Uri.EscapeDataString(title)}";

        var json = await _httpClient.GetStringAsync(url, cancellationToken);
        using var doc = JsonDocument.Parse(json);

        // A missing "parse" property means the API returned an error body (rate-limit, maxlag, a
        // missing page) with a 200 status. Treat it as a failure — throw so the caller falls back to
        // the DB — rather than as "this race has no polls", which would poison the blend.
        if (!doc.RootElement.TryGetProperty("parse", out var parse) ||
            !parse.TryGetProperty("wikitext", out var wt))
        {
            var error = doc.RootElement.TryGetProperty("error", out var err) &&
                        err.TryGetProperty("info", out var info)
                ? info.GetString()
                : "no 'parse' content";
            throw new InvalidOperationException($"MediaWiki parse failed for '{title}': {error}");
        }

        var wikitext = wt.GetString() ?? string.Empty;

        // For a multi-district House race, the state-wide article covers every district; narrow to
        // this district's "District N" section first. Within it we accept only clean two-way tables
        // (see ParseTable's twoWayOnly), which excludes party-primary (D-vs-D / R-vs-R) and top-two
        // jungle-primary (multi-D) tables without needing to guess this state's heading nesting.
        var twoWayOnly = false;
        if (IsMultiDistrictHouse(raceId, out var district))
        {
            var districtSection = ExtractSectionBlock(
                wikitext, t => t.Equals($"District {district}", StringComparison.OrdinalIgnoreCase), 2);
            if (string.IsNullOrWhiteSpace(districtSection))
            {
                _logger.LogDebug("No 'District {District}' section for {RaceId}", district, raceId);
                return new List<PollData>();
            }
            wikitext = districtSection;
            twoWayOnly = true;
        }

        // Isolate the general-election polling section so we don't ingest primary polls. (For the
        // House district case above, the two-way guard already does this; there the "Polling"
        // heading may sit at any depth, so fall through to scanning the whole district section.)
        var geBlock = ExtractSectionBlock(wikitext, t => t.Equals("General election", StringComparison.OrdinalIgnoreCase), 2);
        var pollingBlock = geBlock is not null
            ? ExtractSectionBlock(geBlock, t => t.Equals("Polling", StringComparison.OrdinalIgnoreCase), 3)
            : ExtractSectionBlock(wikitext, t => t.Equals("Polling", StringComparison.OrdinalIgnoreCase), 2);

        // Fall back to the whole (already district-scoped) block when there's no clean Polling
        // heading — top-two states file general head-to-heads under an oddly-named subsection.
        var scanBlock = pollingBlock ?? (twoWayOnly ? wikitext : null);
        if (string.IsNullOrWhiteSpace(scanBlock))
        {
            _logger.LogDebug("No general-election polling section for {RaceId}", raceId);
            return new List<PollData>();
        }

        var polls = new List<PollData>();
        foreach (var table in ExtractTables(scanBlock))
        {
            polls.AddRange(ParseTable(table, raceId, twoWayOnly));
        }

        // A pollster's result is repeated across each hypothetical-matchup table.
        // Keep the first occurrence per (pollster, date) — the actual-nominee matchup is listed first.
        var deduped = polls
            .GroupBy(p => (p.Pollster, p.Date.Date))
            .Select(g => g.First())
            .ToList();

        _logger.LogInformation("Parsed {Count} polls ({Raw} rows) for {RaceId} from Wikipedia",
            deduped.Count, polls.Count, raceId);
        return deduped;
    }

    // ---- Table parsing -----------------------------------------------------

    private List<PollData> ParseTable(string table, string raceId, bool twoWayOnly = false)
    {
        var polls = new List<PollData>();
        var rows = SplitRows(table);
        if (rows.Count < 2) return polls;

        // The header is the first row containing "!" header cells.
        var headerRow = rows.FirstOrDefault(r => r.Any(c => c.IsHeader));
        if (headerRow is null) return polls;
        var headers = headerRow.Select(c => CleanWikiText(c.Content)).ToList();

        // Skip poll-aggregator tables (270toWin/RCP averages) — we compute our own average.
        if (headers.Any(h => h.Contains("aggregation", StringComparison.OrdinalIgnoreCase)))
            return polls;

        int demCol = headers.FindIndex(h => EndsWithParty(h, 'D'));
        int repCol = headers.FindIndex(h => EndsWithParty(h, 'R'));
        if (demCol < 0 || repCol < 0) return polls; // Not a D-vs-R table.

        // In House district sections we scan the whole section (primary + general tables mixed), so
        // only trust a clean two-way general matchup — exactly one Dem and one Rep candidate column.
        // This drops party-primary (D-vs-D / R-vs-R) and top-two jungle-primary (multi-D) tables.
        if (twoWayOnly &&
            (headers.Count(h => EndsWithParty(h, 'D')) != 1 || headers.Count(h => EndsWithParty(h, 'R')) != 1))
            return polls;

        int dateCol = headers.FindIndex(h => h.Contains("administered", StringComparison.OrdinalIgnoreCase)
                                          || h.Contains("Date", StringComparison.OrdinalIgnoreCase));
        int sampleCol = headers.FindIndex(h => h.Contains("Sample", StringComparison.OrdinalIgnoreCase));

        foreach (var row in rows)
        {
            if (row.Any(c => c.IsHeader)) continue; // skip header rows
            var cells = row.Where(c => !c.IsHeader).Select(c => c.Content).ToList();
            if (cells.Count <= Math.Max(demCol, repCol)) continue; // misaligned (rowspan/colspan)

            var demPct = ParsePercent(cells[demCol]);
            var repPct = ParsePercent(cells[repCol]);
            if (demPct is null || repPct is null) continue;

            if (!PollFilters.IsUsableTwoWay(demPct.Value, repPct.Value)) continue;

            var pollsterRaw = cells.Count > 0 ? CleanWikiText(cells[0]) : "";
            if (string.IsNullOrWhiteSpace(pollsterRaw) ||
                pollsterRaw.Equals("Average", StringComparison.OrdinalIgnoreCase) ||
                pollsterRaw.Contains(" vs.", StringComparison.OrdinalIgnoreCase))
                continue;

            var (pollster, partisan) = SplitPartisan(pollsterRaw);

            DateTime? date = dateCol >= 0 && dateCol < cells.Count
                ? ParseEndDate(CleanWikiText(cells[dateCol]))
                : null;
            if (date is null) continue;

            int? sample = null;
            string? population = null;
            if (sampleCol >= 0 && sampleCol < cells.Count)
                (sample, population) = ParseSample(CleanWikiText(cells[sampleCol]));

            polls.Add(new PollData
            {
                RaceId = raceId,
                Pollster = pollster,
                Date = date.Value,
                SampleSize = sample,
                DemPercent = demPct.Value,
                RepPercent = repPct.Value,
                Population = population,
                PollsterRating = PollsterRatings.GetRating(pollster),
                Methodology = partisan is null ? null : $"Partisan ({partisan})"
            });
        }

        return polls;
    }

    /// <summary>A parsed table cell.</summary>
    private readonly record struct Cell(string Content, bool IsHeader);

    /// <summary>Splits a wikitable into rows of cells (best-effort, one cell per line).</summary>
    private static List<List<Cell>> SplitRows(string table)
    {
        var rows = new List<List<Cell>>();
        var current = new List<Cell>();
        var started = false;

        foreach (var raw in table.Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.StartsWith("{|") || line.StartsWith("|}")) continue;

            if (line.StartsWith("|-"))
            {
                if (started) rows.Add(current);
                current = new List<Cell>();
                started = true;
                continue;
            }

            if (line.StartsWith("!"))
            {
                foreach (var part in SplitInline(line.Substring(1), "!!"))
                    current.Add(new Cell(StripCellAttributes(part), true));
            }
            else if (line.StartsWith("|"))
            {
                foreach (var part in SplitInline(line.Substring(1), "||"))
                    current.Add(new Cell(StripCellAttributes(part), false));
            }
        }
        if (current.Count > 0) rows.Add(current);
        return rows;
    }

    /// <summary>Splits a cell line on an inline separator, ignoring separators inside templates/links.</summary>
    private static IEnumerable<string> SplitInline(string s, string sep) =>
        s.Contains(sep) ? s.Split(new[] { sep }, StringSplitOptions.None) : new[] { s };

    /// <summary>
    /// Removes a leading cell attribute segment. In wikitables a cell is
    /// "attributes | content"; the party-shading template also appears as an attribute.
    /// Splits on the first top-level pipe (not inside {{ }} or [[ ]]).
    /// </summary>
    private static string StripCellAttributes(string cell)
    {
        int depthBrace = 0, depthBracket = 0;
        for (int i = 0; i < cell.Length - 0; i++)
        {
            if (i < cell.Length - 1 && cell[i] == '{' && cell[i + 1] == '{') { depthBrace++; i++; continue; }
            if (i < cell.Length - 1 && cell[i] == '}' && cell[i + 1] == '}') { if (depthBrace > 0) depthBrace--; i++; continue; }
            if (i < cell.Length - 1 && cell[i] == '[' && cell[i + 1] == '[') { depthBracket++; i++; continue; }
            if (i < cell.Length - 1 && cell[i] == ']' && cell[i + 1] == ']') { if (depthBracket > 0) depthBracket--; i++; continue; }
            if (cell[i] == '|' && depthBrace == 0 && depthBracket == 0)
                return cell.Substring(i + 1);
        }
        return cell;
    }

    /// <summary>Extracts each "{| ... |}" wikitable from a block of wikitext.</summary>
    private static IEnumerable<string> ExtractTables(string block)
    {
        int idx = 0;
        while (true)
        {
            int start = block.IndexOf("{|", idx, StringComparison.Ordinal);
            if (start < 0) yield break;

            // Match nested tables by depth.
            int depth = 0, i = start;
            while (i < block.Length - 1)
            {
                if (block[i] == '{' && block[i + 1] == '|') { depth++; i += 2; continue; }
                if (block[i] == '|' && block[i + 1] == '}') { depth--; i += 2; if (depth == 0) break; continue; }
                i++;
            }
            yield return block.Substring(start, Math.Min(i, block.Length) - start);
            idx = i;
        }
    }

    // ---- Field parsing helpers --------------------------------------------

    private static bool EndsWithParty(string header, char party) =>
        Regex.IsMatch(header, $@"\({party}\)\s*$");

    /// <summary>Splits "Quantus Insights (R)" into ("Quantus Insights", "R"); non-partisan → (name, null).</summary>
    private static (string Name, string? Partisan) SplitPartisan(string pollster)
    {
        var m = Regex.Match(pollster, @"^(.*?)\s*\(([DRI])(?:-[^)]*)?\)\s*$");
        return m.Success ? (m.Groups[1].Value.Trim(), m.Groups[2].Value) : (pollster.Trim(), null);
    }

    private static double? ParsePercent(string cell)
    {
        var text = CleanWikiText(cell);
        var m = Regex.Match(text, @"(\d+(?:\.\d+)?)\s*%");
        if (!m.Success) m = Regex.Match(text, @"^\s*(\d+(?:\.\d+)?)\s*$");
        return m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)
            ? v : null;
    }

    /// <summary>Parses "947 (LV)" → (947, "LV").</summary>
    private static (int?, string?) ParseSample(string cell)
    {
        int? size = null;
        var sizeMatch = Regex.Match(cell, @"(\d[\d,]*)");
        if (sizeMatch.Success &&
            int.TryParse(sizeMatch.Groups[1].Value.Replace(",", ""), out var s))
            size = s;

        string? pop = null;
        var popMatch = Regex.Match(cell, @"\b(LV|RV|A|V)\b");
        if (popMatch.Success)
            pop = popMatch.Groups[1].Value == "A" ? "A" : popMatch.Groups[1].Value;

        return (size, pop);
    }

    /// <summary>Parses the end date from a range like "June 11–14, 2026" or single "June 17, 2026".</summary>
    private static DateTime? ParseEndDate(string raw)
    {
        var s = Regex.Replace(raw, @"[‒–—−]", "-").Trim();
        s = Regex.Replace(s, @"^\s*(through|as of)\s+", "", RegexOptions.IgnoreCase);
        if (!Regex.IsMatch(s, @"20\d{2}")) return null;

        var parts = s.Split('-');
        var last = parts[^1].Trim();
        // If the final segment lacks a month name, borrow it from the start of the range.
        if (!Regex.IsMatch(last, "[A-Za-z]"))
        {
            var month = Regex.Match(parts[0], "[A-Za-z]+");
            if (month.Success) last = $"{month.Value} {last}";
        }

        foreach (var candidate in new[] { last, s })
        {
            if (DateTime.TryParse(candidate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                return d;
        }
        return null;
    }

    /// <summary>Strips wiki/HTML markup (refs, templates, links, bold, tags) to plain text.</summary>
    private static string CleanWikiText(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;

        // <ref>...</ref> and self-closing <ref/>
        s = Regex.Replace(s, @"<ref[^>]*?/>", "", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<ref[^>]*?>.*?</ref>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // Templates {{...}} — remove innermost repeatedly to handle nesting.
        string prev;
        do { prev = s; s = Regex.Replace(s, @"\{\{[^{}]*\}\}", ""); } while (s != prev);

        // A multi-line template split across table cells leaves an unclosed "{{..." remnant the
        // balanced pass can't match (that's how "UNH{{cite web|url=..." became a pollster name).
        s = Regex.Replace(s, @"\{\{.*$", "", RegexOptions.Singleline);

        // Wiki links [[target|label]] -> label, [[target]] -> target
        s = Regex.Replace(s, @"\[\[[^\]|]*\|([^\]]*)\]\]", "$1");
        s = Regex.Replace(s, @"\[\[([^\]]*)\]\]", "$1");

        s = s.Replace("'''", "").Replace("''", "");
        s = Regex.Replace(s, @"<br\s*/?>", " ", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<[^>]+>", "");           // any remaining HTML
        s = Regex.Replace(s, @"&nbsp;|&#160;", " ");
        s = Regex.Replace(s, @"\s+", " ");
        return s.Trim();
    }

    /// <summary>
    /// Returns the text of the section with the given heading title/level, up to the next
    /// heading of equal-or-higher level. Returns null if not found.
    /// </summary>
    private static string? ExtractSectionBlock(string wikitext, Func<string, bool> titleMatch, int level)
    {
        var lines = wikitext.Split('\n');
        var headingRe = HeadingRegex();
        int start = -1;

        for (int i = 0; i < lines.Length; i++)
        {
            var m = headingRe.Match(lines[i]);
            if (!m.Success) continue;
            int lvl = m.Groups[1].Value.Length;
            var title = m.Groups[2].Value.Trim();

            if (start < 0)
            {
                if (lvl == level && titleMatch(title)) start = i;
            }
            else if (lvl <= level)
            {
                return string.Join("\n", lines.Skip(start + 1).Take(i - start - 1));
            }
        }
        return start >= 0 ? string.Join("\n", lines.Skip(start + 1)) : null;
    }

    // ---- Race -> Wikipedia page title -------------------------------------

    // States with a single at-large House district — their House race lives on the state-wide
    // "...election in {State}" page (Alaska's is the marquee ranked-choice race with real polls).
    private static readonly HashSet<string> AtLargeStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "AK", "DE", "ND", "SD", "VT", "WY"
    };

    private static string? GetPageTitle(string raceId)
    {
        // Statewide is "MI-SEN-2026" / "GA-GOV-2026"; House is "PA-07-2026", where the middle
        // segment is the district number.
        var parts = raceId.Split('-');
        if (parts.Length < 3) return null;
        var abbr = parts[0];
        var kind = parts[1];
        if (!StateNames.TryGetValue(abbr, out var state)) return null;

        if (kind.Equals("SEN", StringComparison.OrdinalIgnoreCase))
            return $"2026 United States Senate election in {state}";
        if (kind.Equals("GOV", StringComparison.OrdinalIgnoreCase))
            return $"2026 {state} gubernatorial election";

        // House: at-large states have their own single-race page ("...election in {State}", singular).
        // Multi-district states put every district's polling in one big state-wide article
        // ("...elections in {State}", plural), under a "District N" section — the per-district
        // sub-articles mostly don't exist yet. FetchAndParse isolates the right district's section.
        if (int.TryParse(kind, out _))
        {
            return AtLargeStates.Contains(abbr)
                ? $"2026 United States House of Representatives election in {state}"
                : $"2026 United States House of Representatives elections in {state}";
        }
        return null;
    }

    /// <summary>True for a multi-district House race (e.g. "PA-07-2026"), whose polls live in a
    /// "District N" section of the state-wide article; sets <paramref name="district"/> to N.</summary>
    private static bool IsMultiDistrictHouse(string raceId, out int district)
    {
        district = 0;
        var parts = raceId.Split('-');
        return parts.Length >= 3
            && int.TryParse(parts[1], out district)
            && !AtLargeStates.Contains(parts[0]);
    }

    [GeneratedRegex(@"^(={2,6})\s*(.*?)\s*\1\s*$")]
    private static partial Regex HeadingRegex();

    private static readonly Dictionary<string, string> StateNames = new(StringComparer.OrdinalIgnoreCase)
    {
        { "AL", "Alabama" }, { "AK", "Alaska" }, { "AZ", "Arizona" }, { "AR", "Arkansas" },
        { "CA", "California" }, { "CO", "Colorado" }, { "CT", "Connecticut" }, { "DE", "Delaware" },
        { "FL", "Florida" }, { "GA", "Georgia" }, { "HI", "Hawaii" }, { "ID", "Idaho" },
        { "IL", "Illinois" }, { "IN", "Indiana" }, { "IA", "Iowa" }, { "KS", "Kansas" },
        { "KY", "Kentucky" }, { "LA", "Louisiana" }, { "ME", "Maine" }, { "MD", "Maryland" },
        { "MA", "Massachusetts" }, { "MI", "Michigan" }, { "MN", "Minnesota" }, { "MS", "Mississippi" },
        { "MO", "Missouri" }, { "MT", "Montana" }, { "NE", "Nebraska" }, { "NV", "Nevada" },
        { "NH", "New Hampshire" }, { "NJ", "New Jersey" }, { "NM", "New Mexico" }, { "NY", "New York" },
        { "NC", "North Carolina" }, { "ND", "North Dakota" }, { "OH", "Ohio" }, { "OK", "Oklahoma" },
        { "OR", "Oregon" }, { "PA", "Pennsylvania" }, { "RI", "Rhode Island" }, { "SC", "South Carolina" },
        { "SD", "South Dakota" }, { "TN", "Tennessee" }, { "TX", "Texas" }, { "UT", "Utah" },
        { "VT", "Vermont" }, { "VA", "Virginia" }, { "WA", "Washington" }, { "WV", "West Virginia" },
        { "WI", "Wisconsin" }, { "WY", "Wyoming" }
    };

    // ---- Persistence -------------------------------------------------------

    private async Task SavePollsToDbAsync(List<PollData> polls, CancellationToken cancellationToken)
    {
        foreach (var poll in polls)
        {
            var exists = await _dbContext.Polls.AnyAsync(p =>
                p.RaceId == poll.RaceId &&
                p.Pollster == poll.Pollster &&
                p.Date == poll.Date, cancellationToken);

            if (!exists)
            {
                _dbContext.Polls.Add(new PollEntity
                {
                    RaceId = poll.RaceId,
                    Pollster = poll.Pollster,
                    Date = poll.Date,
                    SampleSize = poll.SampleSize,
                    DemPercent = poll.DemPercent,
                    RepPercent = poll.RepPercent,
                    PollsterRating = poll.PollsterRating,
                    Methodology = poll.Methodology,
                    Population = poll.Population
                });
            }
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<PollData>> LoadPollsFromDbAsync(string raceId, CancellationToken cancellationToken)
    {
        var entities = await _dbContext.Polls
            .Where(p => p.RaceId == raceId)
            .OrderByDescending(p => p.Date)
            .ToListAsync(cancellationToken);

        return entities.Select(e => new PollData
        {
            RaceId = e.RaceId,
            Pollster = e.Pollster,
            Date = e.Date,
            SampleSize = e.SampleSize,
            DemPercent = e.DemPercent,
            RepPercent = e.RepPercent,
            PollsterRating = e.PollsterRating,
            Methodology = e.Methodology,
            Population = e.Population
        }).ToList();
    }
}
