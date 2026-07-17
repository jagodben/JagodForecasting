using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using ElectionForecaster.Infrastructure.Data;
using ElectionForecaster.Infrastructure.Data.Entities;
using ElectionForecaster.Infrastructure.DataSources.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ElectionForecaster.Infrastructure.DataSources.Polling;

/// <summary>
/// Fetches the generic congressional ballot average by parsing the poll-aggregation table in
/// the "Opinion polling" section of the English Wikipedia article on the 2026 U.S. House
/// elections (via the MediaWiki API). Averages the listed aggregators (Decision Desk HQ,
/// RealClearPolitics, Silver Bulletin, etc.) into a single national Dem margin, persists a
/// daily point, and falls back to the last stored value when the fetch fails.
/// </summary>
public partial class WikipediaGenericBallotClient : IGenericBallotSource
{
    private readonly HttpClient _httpClient;
    private readonly ForecastDbContext _dbContext;
    private readonly ILogger<WikipediaGenericBallotClient> _logger;

    private double? _cachedMargin;
    private DateTime _fetchedAt = DateTime.MinValue;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    private const string ApiBase = "https://en.wikipedia.org/w/api.php";
    private const string PageTitle = "2026 United States House of Representatives elections";

    public WikipediaGenericBallotClient(
        HttpClient httpClient,
        ForecastDbContext dbContext,
        ILogger<WikipediaGenericBallotClient> logger)
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

    public async Task<double?> GetCurrentMarginAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedMargin.HasValue && DateTime.UtcNow - _fetchedAt < CacheTtl)
            return _cachedMargin;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedMargin.HasValue && DateTime.UtcNow - _fetchedAt < CacheTtl)
                return _cachedMargin;

            // Today's stored point is shared across every per-race DI scope, so only the first
            // forecast of the day actually hits Wikipedia; the rest read this row.
            var today = DateTime.UtcNow.Date;
            var todaysRow = await _dbContext.GenericBallot
                .FirstOrDefaultAsync(g => g.Date == today, cancellationToken);
            if (todaysRow != null)
                return CacheAndReturn(todaysRow.Margin);

            try
            {
                var parsed = await FetchAndParseAsync(cancellationToken);
                if (parsed is { } avg)
                {
                    await SaveToDbAsync(avg.DemPercent, avg.RepPercent, cancellationToken);
                    var margin = avg.DemPercent - avg.RepPercent;
                    _logger.LogInformation(
                        "Generic ballot: D {Dem:F1}% / R {Rep:F1}% (D{Margin:+0.0;-0.0}) from {Count} aggregators",
                        avg.DemPercent, avg.RepPercent, margin, avg.Count);
                    return CacheAndReturn(margin);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Generic-ballot fetch failed; falling back to stored value");
            }

            // Fallback: most recent stored point of any date.
            var latest = await _dbContext.GenericBallot
                .OrderByDescending(g => g.Date)
                .FirstOrDefaultAsync(cancellationToken);
            return CacheAndReturn(latest?.Margin);
        }
        finally
        {
            _lock.Release();
        }
    }

    private double? CacheAndReturn(double? margin)
    {
        _cachedMargin = margin;
        _fetchedAt = DateTime.UtcNow;
        return margin;
    }

    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        // Force a re-fetch on next access.
        _fetchedAt = DateTime.MinValue;
        return Task.CompletedTask;
    }

    // ---- Fetch + parse -----------------------------------------------------

    private async Task<(double DemPercent, double RepPercent, int Count)?> FetchAndParseAsync(CancellationToken cancellationToken)
    {
        var url = $"{ApiBase}?action=parse&prop=wikitext&formatversion=2&redirects=1&format=json&page={Uri.EscapeDataString(PageTitle)}";
        var json = await _httpClient.GetStringAsync(url, cancellationToken);
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("parse", out var parse) ||
            !parse.TryGetProperty("wikitext", out var wt))
            return null;

        var wikitext = wt.GetString() ?? string.Empty;

        // The generic-ballot aggregation table is the first wikitable in "Opinion polling".
        var section = ExtractSectionBlock(wikitext, t => t.Equals("Opinion polling", StringComparison.OrdinalIgnoreCase), 2);
        var table = section is null ? null : ExtractTables(section).FirstOrDefault();
        if (table is null) return null;

        var rows = SplitRows(table);
        var headerRow = rows.FirstOrDefault(r => r.Any(c => c.IsHeader));
        if (headerRow is null) return null;
        var headers = headerRow.Select(c => CleanWikiText(c.Content)).ToList();

        int demCol = headers.FindIndex(h => h.StartsWith("Democrat", StringComparison.OrdinalIgnoreCase));
        int repCol = headers.FindIndex(h => h.StartsWith("Republican", StringComparison.OrdinalIgnoreCase));
        if (demCol < 0 || repCol < 0) return null;

        var demVals = new List<double>();
        var repVals = new List<double>();
        foreach (var row in rows)
        {
            if (row.Any(c => c.IsHeader)) continue;
            var cells = row.Where(c => !c.IsHeader).Select(c => c.Content).ToList();
            if (cells.Count == 0) continue;

            // The final "Average" row is colspan-merged (misaligned) — skip it and average the
            // individual aggregators ourselves.
            if (CleanWikiText(cells[0]).Equals("Average", StringComparison.OrdinalIgnoreCase)) continue;
            if (cells.Count <= Math.Max(demCol, repCol)) continue;

            var dem = ParsePercent(cells[demCol]);
            var rep = ParsePercent(cells[repCol]);
            if (dem is null || rep is null) continue;

            demVals.Add(dem.Value);
            repVals.Add(rep.Value);
        }

        if (demVals.Count == 0) return null;
        return (demVals.Average(), repVals.Average(), demVals.Count);
    }

    private async Task SaveToDbAsync(double demPercent, double repPercent, CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var existing = await _dbContext.GenericBallot.FirstOrDefaultAsync(g => g.Date == today, cancellationToken);
        if (existing is null)
        {
            _dbContext.GenericBallot.Add(new GenericBallotEntity
            {
                Date = today,
                DemPercent = demPercent,
                RepPercent = repPercent,
                Source = "Wikipedia (aggregate)"
            });
        }
        else
        {
            existing.DemPercent = demPercent;
            existing.RepPercent = repPercent;
            existing.Source = "Wikipedia (aggregate)";
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    // ---- Wikitext parsing (self-contained; mirrors WikipediaPollingClient) --

    private readonly record struct Cell(string Content, bool IsHeader);

    /// <summary>Splits a wikitable into rows of cells (best-effort, one cell per line).</summary>
    private static List<List<Cell>> SplitRows(string table)
    {
        var rows = new List<List<Cell>>();
        var current = new List<Cell>();

        foreach (var raw in table.Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.StartsWith("{|") || line.StartsWith("|}")) continue;
            if (line.StartsWith("|+")) continue; // caption
            if (line.StartsWith("|-"))
            {
                if (current.Count > 0) rows.Add(current);
                current = new List<Cell>();
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

    private static IEnumerable<string> SplitInline(string s, string sep) =>
        s.Contains(sep) ? s.Split(new[] { sep }, StringSplitOptions.None) : new[] { s };

    /// <summary>
    /// Removes a leading cell attribute segment ("attributes | content"), splitting on the
    /// first top-level pipe (not inside {{ }} or [[ ]]).
    /// </summary>
    private static string StripCellAttributes(string cell)
    {
        int depthBrace = 0, depthBracket = 0;
        for (int i = 0; i < cell.Length; i++)
        {
            if (i < cell.Length - 1 && cell[i] == '{' && cell[i + 1] == '{') { depthBrace++; i++; continue; }
            if (i < cell.Length - 1 && cell[i] == '}' && cell[i + 1] == '}') { if (depthBrace > 0) depthBrace--; i++; continue; }
            if (i < cell.Length - 1 && cell[i] == '[' && cell[i + 1] == '[') { depthBracket++; i++; continue; }
            if (i < cell.Length - 1 && cell[i] == ']' && cell[i + 1] == ']') { if (depthBracket > 0) depthBracket--; i++; continue; }
            if (cell[i] == '|' && depthBrace == 0 && depthBracket == 0) return cell.Substring(i + 1);
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

    private static double? ParsePercent(string cell)
    {
        var text = CleanWikiText(cell);
        var m = Regex.Match(text, @"(\d+(?:\.\d+)?)\s*%");
        if (!m.Success) m = Regex.Match(text, @"^\s*(\d+(?:\.\d+)?)\s*$");
        return m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)
            ? v : null;
    }

    /// <summary>Strips wiki/HTML markup (refs, templates, links, bold, tags) to plain text.</summary>
    private static string CleanWikiText(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;

        s = Regex.Replace(s, @"<ref[^>]*?/>", "", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<ref[^>]*?>.*?</ref>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        string prev;
        do { prev = s; s = Regex.Replace(s, @"\{\{[^{}]*\}\}", ""); } while (s != prev);

        s = Regex.Replace(s, @"\[\[[^\]|]*\|([^\]]*)\]\]", "$1");
        s = Regex.Replace(s, @"\[\[([^\]]*)\]\]", "$1");

        s = s.Replace("'''", "").Replace("''", "");
        s = Regex.Replace(s, @"<br\s*/?>", " ", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<[^>]+>", "");
        s = Regex.Replace(s, @"&nbsp;|&#160;", " ");
        s = Regex.Replace(s, @"\s+", " ");
        return s.Trim();
    }

    [GeneratedRegex(@"^(={2,6})\s*(.*?)\s*\1\s*$")]
    private static partial Regex HeadingRegex();
}
