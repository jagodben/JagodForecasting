using System.Text.Json;

// Discovers the Polymarket market ID for each 2026 Senate/Governor race via the Gamma API, so the
// RaceIdToMarketId map in PolymarketClient no longer has to be hand-collected from the network tab.
// Build-time / one-off tool: run it, review the output, paste the block into PolymarketClient.
//
//   dotnet run --project ElectionForecaster.MarketDiscovery
//
// Algorithm per race: find the general-election "winner" event (by slug, then public-search),
// take the market whose groupItemTitle == "Democrat" from the open event with the most volume.
// House is intentionally excluded — Polymarket has no per-district general-election markets.

const string ApiBase = "https://gamma-api.polymarket.com";

var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
http.DefaultRequestHeaders.UserAgent.ParseAdd("ElectionForecaster-MarketDiscovery/1.0");

// States with a 2026 race (mirrors ElectionDataProvider's HasSenateRace / HasGovRace flags).
var senateStates = "AL AK AR CO DE GA ID IL IA KS KY LA ME MA MI MN MS MT NE NH NJ NM NC OK OR RI SC SD TN TX VA WV WY".Split(' ');
var govStates = "AL AK AZ AR CA CO CT FL GA HI ID IL IA KS ME MD MA MI MN NE NV NH NM NY OH OK OR PA RI SC SD TN TX VT WI WY".Split(' ');

var stateNames = new Dictionary<string, string>
{
    ["AL"] = "Alabama", ["AK"] = "Alaska", ["AZ"] = "Arizona", ["AR"] = "Arkansas", ["CA"] = "California",
    ["CO"] = "Colorado", ["CT"] = "Connecticut", ["DE"] = "Delaware", ["FL"] = "Florida", ["GA"] = "Georgia",
    ["HI"] = "Hawaii", ["ID"] = "Idaho", ["IL"] = "Illinois", ["IN"] = "Indiana", ["IA"] = "Iowa",
    ["KS"] = "Kansas", ["KY"] = "Kentucky", ["LA"] = "Louisiana", ["ME"] = "Maine", ["MD"] = "Maryland",
    ["MA"] = "Massachusetts", ["MI"] = "Michigan", ["MN"] = "Minnesota", ["MS"] = "Mississippi", ["MO"] = "Missouri",
    ["MT"] = "Montana", ["NE"] = "Nebraska", ["NV"] = "Nevada", ["NH"] = "New Hampshire", ["NJ"] = "New Jersey",
    ["NM"] = "New Mexico", ["NY"] = "New York", ["NC"] = "North Carolina", ["ND"] = "North Dakota", ["OH"] = "Ohio",
    ["OK"] = "Oklahoma", ["OR"] = "Oregon", ["PA"] = "Pennsylvania", ["RI"] = "Rhode Island", ["SC"] = "South Carolina",
    ["SD"] = "South Dakota", ["TN"] = "Tennessee", ["TX"] = "Texas", ["UT"] = "Utah", ["VT"] = "Vermont",
    ["VA"] = "Virginia", ["WA"] = "Washington", ["WV"] = "West Virginia", ["WI"] = "Wisconsin", ["WY"] = "Wyoming",
};

string Slugify(string name) => name.ToLowerInvariant().Replace(' ', '-');

var resolved = new List<(string raceId, string marketId)>();
var warnings = new List<string>();

foreach (var (states, office) in new[] { (senateStates, "SEN"), (govStates, "GOV") })
{
    foreach (var abbr in states)
    {
        var raceId = $"{abbr}-{office}-2026";
        try
        {
            var (marketId, note) = await DiscoverAsync(abbr, office);
            if (marketId != null)
            {
                resolved.Add((raceId, marketId));
                Console.Error.WriteLine($"  ok   {raceId} -> {marketId}");
            }
            else
            {
                warnings.Add($"{raceId}: {note}");
                Console.Error.WriteLine($"  MISS {raceId}: {note}");
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"{raceId}: error {ex.Message}");
            Console.Error.WriteLine($"  ERR  {raceId}: {ex.Message}");
        }
        await Task.Delay(60); // be polite to the API
    }
}

// ---- Output: paste-ready C# block + warnings ----
Console.WriteLine();
Console.WriteLine("// ===== Verified RaceIdToMarketId (generated) =====");
foreach (var (raceId, marketId) in resolved)
    Console.WriteLine($"        {{ \"{raceId}\", \"{marketId}\" }},");
Console.WriteLine($"// {resolved.Count} resolved, {warnings.Count} unresolved");
if (warnings.Count > 0)
{
    Console.WriteLine("\n// Unresolved (no clean two-party market — verify manually or leave existing):");
    foreach (var w in warnings) Console.WriteLine($"//   {w}");
}

// ---------------------------------------------------------------------------

async Task<(string? marketId, string note)> DiscoverAsync(string abbr, string office)
{
    var stateName = stateNames[abbr];
    var stateSlug = Slugify(stateName);
    var officeSlug = office == "SEN" ? "senate" : "governor";

    // Candidate event IDs, from direct slug patterns then a public-search fallback.
    var candidateIds = new HashSet<string>();

    var directSlugs = office == "SEN"
        ? new[] { $"{stateSlug}-senate-election-winner", $"{stateSlug}-us-senate-election-winner" }
        : new[] { $"{stateSlug}-governor-winner-2026", $"{stateSlug}-governor-election-winner" };

    foreach (var slug in directSlugs)
        foreach (var ev in await GetEventsAsync($"{ApiBase}/events?slug={slug}"))
            if (ev.TryGetProperty("id", out var idEl) && JsonId(idEl) is { } id) candidateIds.Add(id);

    var query = Uri.EscapeDataString($"{stateName} {(office == "SEN" ? "Senate" : "Governor")} winner");
    using (var search = await GetJsonAsync($"{ApiBase}/public-search?q={query}&limit_per_type=15"))
    {
        if (search.RootElement.TryGetProperty("events", out var evs) && evs.ValueKind == JsonValueKind.Array)
            foreach (var ev in evs.EnumerateArray())
            {
                var slug = ev.TryGetProperty("slug", out var s) ? s.GetString() ?? "" : "";
                if (IsGeneralWinner(slug, officeSlug, stateSlug) && ev.TryGetProperty("id", out var idEl) && JsonId(idEl) is { } id)
                    candidateIds.Add(id);
            }
    }

    // Score each candidate: must be an open event with an open "Democrat" market; prefer most volume.
    string? bestMarket = null;
    double bestVolume = -1;
    bool sawEvent = false;

    foreach (var id in candidateIds)
    {
        using var doc = await GetJsonAsync($"{ApiBase}/events/{id}");
        var ev = doc.RootElement;
        sawEvent = true;
        if (ev.TryGetProperty("closed", out var c) && c.ValueKind == JsonValueKind.True) continue;

        var demMarket = DemocratMarketId(ev);
        if (demMarket == null) continue;

        double vol = ev.TryGetProperty("volume", out var v) ? JsonNum(v) : 0;
        if (vol > bestVolume) { bestVolume = vol; bestMarket = demMarket; }
    }

    if (bestMarket != null) return (bestMarket, "ok");
    if (sawEvent) return (null, "event(s) found but no open two-party 'Democrat' market");
    return (null, "no matching event found");
}

// A general-election winner event for the right office/state (not a primary/runoff/who-controls market).
bool IsGeneralWinner(string slug, string officeSlug, string stateSlug)
{
    if (!slug.StartsWith(stateSlug + "-")) return false;
    if (!slug.Contains(officeSlug)) return false;
    if (!slug.Contains("winner")) return false;
    string[] bad = { "primary", "runoff", "which-party", "margin", "nominee", "-by-" };
    return !bad.Any(slug.Contains);
}

// The market id for the Democratic side, from an event's non-closed markets. Handles both event
// shapes: party markets ("Democrat" / "Republican" Yes/No) and candidate markets whose title ends
// in "(D)" (e.g. "James Talarico (D)"). If several Democratic markets exist, take the most liquid.
string? DemocratMarketId(JsonElement ev)
{
    if (!ev.TryGetProperty("markets", out var markets) || markets.ValueKind != JsonValueKind.Array) return null;

    string? best = null;
    double bestVol = -1;
    foreach (var m in markets.EnumerateArray())
    {
        if (m.TryGetProperty("closed", out var mc) && mc.ValueKind == JsonValueKind.True) continue;
        var git = (m.TryGetProperty("groupItemTitle", out var g) ? g.GetString() : null)?.Trim();
        if (git == null) continue;

        bool isDem = string.Equals(git, "Democrat", StringComparison.OrdinalIgnoreCase)
                     || git.EndsWith("(D)", StringComparison.OrdinalIgnoreCase);
        if (!isDem) continue;
        if (!m.TryGetProperty("id", out var idEl) || JsonId(idEl) is not { } id) continue;

        double vol = m.TryGetProperty("volume", out var mv) ? JsonNum(mv) : 0;
        if (vol > bestVol) { bestVol = vol; best = id; }
    }
    return best;
}

// Gamma returns ids as numbers and volume sometimes as strings — read either shape safely.
string? JsonId(JsonElement e) => e.ValueKind switch
{
    JsonValueKind.String => e.GetString(),
    JsonValueKind.Number => e.GetRawText(),
    _ => null
};

double JsonNum(JsonElement e) => e.ValueKind switch
{
    JsonValueKind.Number => e.GetDouble(),
    JsonValueKind.String => double.TryParse(e.GetString(), out var d) ? d : 0,
    _ => 0
};

async Task<List<JsonElement>> GetEventsAsync(string url)
{
    using var doc = await GetJsonAsync(url);
    // /events?slug= returns an array; clone elements so they survive the document being disposed.
    return doc.RootElement.ValueKind == JsonValueKind.Array
        ? doc.RootElement.EnumerateArray().Select(e => e.Clone()).ToList()
        : new List<JsonElement>();
}

async Task<JsonDocument> GetJsonAsync(string url)
{
    var json = await http.GetStringAsync(url);
    return JsonDocument.Parse(json);
}
