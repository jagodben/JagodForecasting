using System.Text.Json;
using System.Text.RegularExpressions;
using ElectionForecaster.Core.Enums;
using ElectionForecaster.Core.Models;
using Microsoft.Extensions.Logging;

namespace ElectionForecaster.Infrastructure.DataSources.Candidates;

/// <summary>
/// Scrapes each race's general-election nominees from the {{Infobox election}} on its English
/// Wikipedia article, so the daily refresh can keep candidates current as primaries conclude or
/// candidates change. Statewide races have their own article; House nominees come from the
/// per-district infoboxes inside the state-wide "...elections in {State}" article (at-large
/// states use their single-race article). A side listed with a literal placeholder ("TBD") is
/// reported as explicitly unresolved — a dropout/reset that callers should act on — while a
/// missing or unparseable side yields a plain null the caller ignores.
/// </summary>
public partial class WikipediaCandidateClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WikipediaCandidateClient> _logger;

    private const string ApiBase = "https://en.wikipedia.org/w/api.php";
    // Polite crawl delay between page fetches (~120 pages once a day).
    private static readonly TimeSpan FetchDelay = TimeSpan.FromMilliseconds(150);

    public WikipediaCandidateClient(HttpClient httpClient, ILogger<WikipediaCandidateClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "ElectionForecaster/1.0 (https://jagodforecasting.com; contact via site)");
        }
    }

    /// <summary>A scraped nominee; Incumbent is true when they match the infobox's officeholder.</summary>
    public sealed record ScrapedNominee(string Name, bool IsIncumbent);

    /// <summary>
    /// Nominees for one race. A null side with its ExplicitlyUnresolved flag SET means the infobox
    /// parsed fine and deliberately lists that side as TBD — a real editorial statement (a dropout
    /// or a reset nomination), which callers should treat as "there is no nominee anymore". A null
    /// side WITHOUT the flag just means nothing parseable was found — keep whatever you had.
    /// </summary>
    public sealed record RaceNominees(
        ScrapedNominee? Dem, ScrapedNominee? Rep,
        bool DemExplicitlyUnresolved = false, bool RepExplicitlyUnresolved = false);

    /// <summary>
    /// Fetches nominees for every given race. Returns a map of raceId → nominees; races whose
    /// page is missing or has no parseable infobox are absent from the result.
    /// </summary>
    public async Task<Dictionary<string, RaceNominees>> FetchAllAsync(
        IReadOnlyCollection<Race> races, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, RaceNominees>();

        // Statewide races: one article per race.
        foreach (var race in races.Where(r => r.Type is RaceType.Senate or RaceType.Governor))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var wikitext = await FetchFirstAvailableAsync(StatewideTitles(race), cancellationToken);
            if (wikitext is null)
            {
                _logger.LogWarning("No Wikipedia article found for {RaceId}", race.Id);
                continue;
            }
            var nominees = ParseInfoboxNominees(wikitext);
            if (nominees is not null) result[race.Id] = nominees;
        }

        // House races: all of a state's districts share the state-wide article.
        foreach (var stateGroup in races.Where(r => r.Type == RaceType.House && r.DistrictNumber.HasValue)
                                        .GroupBy(r => r.StateId))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!StateNames.TryGetValue(stateGroup.Key, out var state)) continue;

            var atLarge = stateGroup.Count() == 1;
            var title = atLarge
                ? $"2026 United States House of Representatives election in {state}"
                : $"2026 United States House of Representatives elections in {state}";
            var wikitext = await FetchPageAsync(title, cancellationToken);
            if (wikitext is null)
            {
                _logger.LogWarning("No Wikipedia article found for House races in {State}", state);
                continue;
            }

            foreach (var race in stateGroup)
            {
                var section = atLarge
                    ? wikitext // the whole article is the single race
                    : ExtractDistrictSection(wikitext, race.DistrictNumber!.Value);
                if (section is null) continue;

                var nominees = ParseInfoboxNominees(section);
                if (nominees is not null) result[race.Id] = nominees;
            }
        }

        return result;
    }

    /// <summary>Candidate article titles for a statewide race, most likely first.</summary>
    private static IEnumerable<string> StatewideTitles(Race race)
    {
        StateNames.TryGetValue(race.StateId, out var state);
        if (state is null) yield break;

        if (race.Type == RaceType.Senate)
        {
            // FL/OH 2026 are special elections; their articles may carry either title form.
            if (race.IsSpecialElection)
                yield return $"2026 United States Senate special election in {state}";
            yield return $"2026 United States Senate election in {state}";
        }
        else
        {
            yield return $"2026 {state} gubernatorial election";
        }
    }

    private async Task<string?> FetchFirstAvailableAsync(IEnumerable<string> titles, CancellationToken ct)
    {
        foreach (var title in titles)
        {
            var text = await FetchPageAsync(title, ct);
            if (text is not null) return text;
        }
        return null;
    }

    private async Task<string?> FetchPageAsync(string title, CancellationToken ct)
    {
        await Task.Delay(FetchDelay, ct);
        var url = $"{ApiBase}?action=parse&prop=wikitext&formatversion=2&redirects=1&format=json&page={Uri.EscapeDataString(title)}";
        var json = await _httpClient.GetStringAsync(url, ct);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("parse", out var parse) ||
            !parse.TryGetProperty("wikitext", out var wt))
            return null; // missing page or API error body
        return wt.GetString();
    }

    // ---- Wikitext parsing ---------------------------------------------------

    /// <summary>The wikitext of the L2 "District N" section, or null when absent.</summary>
    private static string? ExtractDistrictSection(string wikitext, int district)
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
                if (lvl == 2 && title.Equals($"District {district}", StringComparison.OrdinalIgnoreCase))
                    start = i;
            }
            else if (lvl <= 2)
            {
                return string.Join("\n", lines.Skip(start + 1).Take(i - start - 1));
            }
        }
        return start >= 0 ? string.Join("\n", lines.Skip(start + 1)) : null;
    }

    /// <summary>
    /// Parses the first {{Infobox election}} in the block into Dem/Rep nominees. Uses the
    /// nominee1..n/party1..n parameters (candidate1..n as a fallback — pre-primary pages use it),
    /// and marks a nominee incumbent when they match the infobox's current officeholder
    /// (before_election). Returns null when there's no infobox at all.
    /// </summary>
    private RaceNominees? ParseInfoboxNominees(string block)
    {
        var infobox = ExtractInfobox(block);
        if (infobox is null) return null;

        var args = ParseTemplateArguments(infobox);

        var incumbentName = CleanValue(args.GetValueOrDefault("before_election") ?? "");

        foreach (var prefix in new[] { "nominee", "candidate" })
        {
            var dems = new List<ScrapedNominee>();
            var reps = new List<ScrapedNominee>();
            bool demTbd = false, repTbd = false;
            for (int i = 1; i <= 9; i++)
            {
                var rawName = args.GetValueOrDefault($"{prefix}{i}");
                var rawParty = args.GetValueOrDefault($"party{i}");
                if (rawName is null || rawParty is null) continue;

                var isDem = rawParty.Contains("Democratic", StringComparison.OrdinalIgnoreCase);
                var isRep = rawParty.Contains("Republican", StringComparison.OrdinalIgnoreCase);
                var name = CleanValue(rawName);

                // A literal placeholder ("TBD") on a parsed infobox is a deliberate statement that
                // this party currently has no nominee — e.g. the named candidate dropped out.
                if (IsExplicitPlaceholder(name))
                {
                    if (isDem) demTbd = true;
                    else if (isRep) repTbd = true;
                    continue;
                }
                if (!IsResolvedName(name)) continue;

                var incumbent = incumbentName.Length > 0 &&
                                name.Equals(incumbentName, StringComparison.OrdinalIgnoreCase);

                if (isDem) dems.Add(new ScrapedNominee(name, incumbent));
                else if (isRep) reps.Add(new ScrapedNominee(name, incumbent));
            }

            // A side is trusted only when the infobox names exactly ONE candidate for that party —
            // two same-party entries means a primary/jungle listing, not a general-election nominee.
            if (dems.Count > 0 || reps.Count > 0 || demTbd || repTbd)
                return new RaceNominees(
                    dems.Count == 1 ? dems[0] : null,
                    reps.Count == 1 ? reps[0] : null,
                    DemExplicitlyUnresolved: demTbd && dems.Count == 0,
                    RepExplicitlyUnresolved: repTbd && reps.Count == 0);
            // Nothing under nomineeN — fall through and retry with candidateN.
        }

        return new RaceNominees(null, null);
    }

    /// <summary>Extracts the body of the first {{Infobox election ...}} (balanced braces), or null.</summary>
    private static string? ExtractInfobox(string block)
    {
        var start = block.IndexOf("{{Infobox election", StringComparison.OrdinalIgnoreCase);
        if (start < 0) return null;

        int depth = 0, i = start;
        while (i < block.Length - 1)
        {
            if (block[i] == '{' && block[i + 1] == '{') { depth++; i += 2; continue; }
            if (block[i] == '}' && block[i + 1] == '}')
            {
                depth--; i += 2;
                if (depth == 0) return block.Substring(start, i - start);
                continue;
            }
            i++;
        }
        return null;
    }

    /// <summary>
    /// Splits a template's body into named arguments on top-level pipes (a pipe inside a nested
    /// {{template}} or [[link]] doesn't split). Multiple parameters may share one source line.
    /// </summary>
    private static Dictionary<string, string> ParseTemplateArguments(string template)
    {
        var args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int braces = 0, brackets = 0, segStart = 0;
        var segments = new List<string>();

        for (int i = 0; i < template.Length; i++)
        {
            if (i < template.Length - 1)
            {
                if (template[i] == '{' && template[i + 1] == '{') { braces++; i++; continue; }
                if (template[i] == '}' && template[i + 1] == '}') { braces--; i++; continue; }
                if (template[i] == '[' && template[i + 1] == '[') { brackets++; i++; continue; }
                if (template[i] == ']' && template[i + 1] == ']') { brackets--; i++; continue; }
            }
            // braces==1: inside the infobox itself but not a nested template.
            if (template[i] == '|' && braces == 1 && brackets == 0)
            {
                segments.Add(template.Substring(segStart, i - segStart));
                segStart = i + 1;
            }
        }
        segments.Add(template.Substring(segStart));

        foreach (var segment in segments.Skip(1)) // segment 0 is "{{Infobox election"
        {
            var eq = segment.IndexOf('=');
            if (eq <= 0) continue;
            var key = segment.Substring(0, eq).Trim();
            var value = segment.Substring(eq + 1).Trim();
            if (key.Length > 0) args[key] = value;
        }
        return args;
    }

    /// <summary>Strips wiki markup and status qualifiers, leaving the bare candidate name.</summary>
    private static string CleanValue(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;

        // Editor comments first — they may contain '>' and confuse the generic tag stripper.
        s = Regex.Replace(s, @"<!--.*?-->", "", RegexOptions.Singleline);
        s = Regex.Replace(s, @"<ref[^>]*?/>", "", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<ref[^>]*?>.*?</ref>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // Nested templates ({{efn}}, {{small}}, ...) — innermost out.
        string prev;
        do { prev = s; s = Regex.Replace(s, @"\{\{[^{}]*\}\}", ""); } while (s != prev);

        s = Regex.Replace(s, @"\[\[[^\]|]*\|([^\]]*)\]\]", "$1"); // [[target|label]] -> label
        s = Regex.Replace(s, @"\[\[([^\]]*)\]\]", "$1");          // [[target]] -> target
        s = Regex.Replace(s, @"<br\s*/?>", " ", RegexOptions.IgnoreCase);
        s = s.Replace("'''", "").Replace("''", "");
        s = Regex.Replace(s, @"<[^>]+>", "");
        s = Regex.Replace(s, @"&nbsp;|&#160;", " ");

        // Status qualifiers appended to the name — "(presumptive)", "(Uncontested)", "(write-in)",
        // etc. A cleaned name never legitimately ends in a parenthetical (page-title disambiguators
        // live in link targets, which the link cleanup above already reduced to labels).
        s = Regex.Replace(s, @"\s*\([^)]*\)\s*$", "");

        return Regex.Replace(s, @"\s+", " ").Trim();
    }

    /// <summary>
    /// True for the literal placeholder values editors use to say "no nominee (currently)" —
    /// "TBD", "To be determined", … A match on a parsed infobox is a deliberate statement (not a
    /// parse failure), so it clears a previously-stored nominee (dropouts, reset nominations).
    /// </summary>
    private static bool IsExplicitPlaceholder(string name)
    {
        if (name.Length == 0) return false; // empty is scaffolding, not a statement
        var upper = name.ToUpperInvariant();
        return upper is "TBD" or "TBA" or "N/A" or "NONE" or "PENDING" or "VACANT"
            || upper.StartsWith("TO BE ");
    }

    /// <summary>False for anything that doesn't look like an actual candidate name.</summary>
    private static bool IsResolvedName(string name)
    {
        if (name.Length < 3 || IsExplicitPlaceholder(name)) return false;
        // A real name has at least two word parts ("Mike Rogers"); guards against stray markup.
        return name.Contains(' ');
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
}
