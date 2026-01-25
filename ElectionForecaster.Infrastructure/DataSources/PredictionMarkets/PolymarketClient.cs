using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ElectionForecaster.Infrastructure.Data;
using ElectionForecaster.Infrastructure.Data.Entities;
using ElectionForecaster.Infrastructure.DataSources.Interfaces;
using ElectionForecaster.Infrastructure.DataSources.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ElectionForecaster.Infrastructure.DataSources.PredictionMarkets;

/// <summary>
/// Client for fetching prediction market data from Polymarket.
/// </summary>
public class PolymarketClient : IPredictionMarketSource
{
    private readonly HttpClient _httpClient;
    private readonly ForecastDbContext _dbContext;
    private readonly ILogger<PolymarketClient> _logger;
    private readonly Dictionary<string, MarketOdds> _cachedOdds = new();
    private DateTime _lastRefresh = DateTime.MinValue;

    // Polymarket API endpoints
    private const string GammaApiBaseUrl = "https://gamma-api.polymarket.com";

    // State abbreviation to full name mapping for Polymarket title matching
    private static readonly Dictionary<string, string> StateIdToName = new()
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

    public string SourceName => "Polymarket";

    public PolymarketClient(
        HttpClient httpClient,
        ForecastDbContext dbContext,
        ILogger<PolymarketClient> logger)
    {
        _httpClient = httpClient;
        _dbContext = dbContext;
        _logger = logger;
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    public async Task<MarketOdds?> GetRaceOddsAsync(string raceId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("PolymarketClient.GetRaceOddsAsync called for {RaceId}", raceId);

        // First check cache
        if (_cachedOdds.TryGetValue(raceId, out var cached) &&
            (DateTime.UtcNow - cached.Timestamp).TotalMinutes < 15)
        {
            _logger.LogDebug("Returning cached odds for {RaceId}", raceId);
            return cached;
        }

        // Check database for recent data
        var recentOdds = await _dbContext.MarketOdds
            .Where(m => m.RaceId == raceId && m.Source == SourceName)
            .OrderByDescending(m => m.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);

        if (recentOdds != null && (DateTime.UtcNow - recentOdds.Timestamp).TotalMinutes < 15)
        {
            _logger.LogDebug("Returning database odds for {RaceId}", raceId);
            var odds = EntityToModel(recentOdds);
            _cachedOdds[raceId] = odds;
            return odds;
        }

        // Console.WriteLine($"[Polymarket] Fetching fresh odds for {raceId}");
        _logger.LogInformation("Fetching fresh odds from Polymarket for {RaceId}", raceId);
        // Try to fetch fresh data by searching for the market
        var freshOdds = await FetchMarketOddsByRaceIdAsync(raceId, cancellationToken);
        // Console.WriteLine($"[Polymarket] Result: {(freshOdds != null ? $"Dem={freshOdds.DemOdds:P1}" : "NULL")}");
        if (freshOdds != null)
        {
            _logger.LogInformation("Got fresh odds for {RaceId}: Dem={Dem:P1}, Rep={Rep:P1}",
                raceId, freshOdds.DemOdds, freshOdds.RepOdds);
            _cachedOdds[raceId] = freshOdds;
            await SaveOddsToDbAsync(freshOdds, cancellationToken);
            return freshOdds;
        }

        _logger.LogWarning("Could not get any odds for {RaceId}", raceId);

        // Return cached or database data even if stale
        if (recentOdds != null)
        {
            return EntityToModel(recentOdds);
        }

        return null;
    }

    public async Task<Dictionary<string, MarketOdds>> GetAllRaceOddsAsync(CancellationToken cancellationToken = default)
    {
        // Return cached data if fresh
        if ((DateTime.UtcNow - _lastRefresh).TotalMinutes < 15 && _cachedOdds.Count > 0)
        {
            return new Dictionary<string, MarketOdds>(_cachedOdds);
        }

        await RefreshAsync(cancellationToken);
        return new Dictionary<string, MarketOdds>(_cachedOdds);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Refreshing Polymarket data...");

        try
        {
            // Fetch all election-related markets
            var markets = await FetchElectionMarketsAsync(cancellationToken);

            foreach (var market in markets)
            {
                var raceId = MapMarketToRaceId(market);
                if (raceId != null)
                {
                    var odds = ParseMarketToOdds(market, raceId);
                    if (odds != null)
                    {
                        _cachedOdds[raceId] = odds;
                        await SaveOddsToDbAsync(odds, cancellationToken);
                    }
                }
            }

            _lastRefresh = DateTime.UtcNow;
            _logger.LogInformation("Polymarket refresh complete. {Count} markets updated.", _cachedOdds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing Polymarket data");
        }
    }

    private async Task<List<PolymarketEvent>> FetchElectionMarketsAsync(CancellationToken cancellationToken)
    {
        var results = new List<PolymarketEvent>();

        try
        {
            // Fetch all politics-tagged events and filter for election markets
            // Polymarket titles follow these patterns:
            // - Senate: "{State} Senate election winner"
            // - House: "{ST}-{##} House Election Winner"
            // - Governor: "{State} Governor Election Winner"
            var url = $"{GammaApiBaseUrl}/events?tag=politics&_limit=200&closed=false";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var events = await response.Content.ReadFromJsonAsync<List<PolymarketEvent>>(
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                    cancellationToken);

                if (events != null)
                {
                    // Filter for election winner markets
                    results.AddRange(events.Where(e =>
                        e.Title != null && (
                            e.Title.Contains("Senate election winner", StringComparison.OrdinalIgnoreCase) ||
                            e.Title.Contains("House Election Winner", StringComparison.OrdinalIgnoreCase) ||
                            e.Title.Contains("Governor Election Winner", StringComparison.OrdinalIgnoreCase)
                        )));
                }

                _logger.LogInformation("Fetched {Total} politics events, {Filtered} are election markets",
                    events?.Count ?? 0, results.Count);
            }
            else
            {
                _logger.LogWarning("Polymarket API returned {Status}: {Reason}",
                    response.StatusCode, response.ReasonPhrase);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching election markets from Polymarket");
        }

        return results.DistinctBy(e => e.Id).ToList();
    }

    private async Task<MarketOdds?> FetchMarketOddsByRaceIdAsync(string raceId, CancellationToken cancellationToken)
    {
        try
        {
            // Parse the race ID to build the slug
            // Race ID format: "NC-SEN-2026", "NC-GOV-2026", or "NC-01-2026" (House)
            var parts = raceId.Split('-');
            if (parts.Length < 3) return null;

            var stateId = parts[0];
            var raceType = parts[1];

            string? slug;
            if (int.TryParse(raceType, out var district))
            {
                // House race: "nc-01-house-election-winner"
                slug = $"{stateId.ToLowerInvariant()}-{district:D2}-house-election-winner";
            }
            else if (StateIdToName.TryGetValue(stateId, out var stateName))
            {
                // Senate or Governor race - slug format: "north-carolina-senate-election-winner"
                var stateSlug = stateName.ToLowerInvariant().Replace(" ", "-");
                slug = raceType.ToUpperInvariant() switch
                {
                    "SEN" => $"{stateSlug}-senate-election-winner",
                    "GOV" => $"{stateSlug}-governor-election-winner",
                    _ => null
                };
            }
            else
            {
                return null;
            }

            if (slug == null) return null;

            // Console.WriteLine($"[Polymarket] Fetching slug: {slug}");
            _logger.LogDebug("Fetching Polymarket event with slug: {Slug}", slug);

            // Fetch directly by slug
            var url = $"{GammaApiBaseUrl}/events?slug={slug}";
            // Console.WriteLine($"[Polymarket] URL: {url}");

            var response = await _httpClient.GetAsync(url, cancellationToken);
            // Console.WriteLine($"[Polymarket] Response: {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Polymarket API returned {Status} for slug {Slug}", response.StatusCode, slug);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            // Console.WriteLine($"[Polymarket] Content length: {content.Length} chars");

            var events = System.Text.Json.JsonSerializer.Deserialize<List<PolymarketEvent>>(
                content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var matchingEvent = events?.FirstOrDefault();
            // Console.WriteLine($"[Polymarket] Found event: {matchingEvent?.Title ?? "NULL"}");
            // Console.WriteLine($"[Polymarket] Markets count: {matchingEvent?.Markets?.Count ?? 0}");

            if (matchingEvent == null)
            {
                _logger.LogDebug("No Polymarket event found for slug: {Slug}", slug);
                return null;
            }

            _logger.LogInformation("Found Polymarket market for {RaceId}: {Title}", raceId, matchingEvent.Title);
            return ParseMarketToOdds(matchingEvent, raceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching market odds for {RaceId}", raceId);
            return null;
        }
    }

    private MarketOdds? ParseMarketToOdds(PolymarketEvent market, string raceId)
    {
        if (market.Markets == null || market.Markets.Count == 0)
            return null;

        // Polymarket election markets have separate sub-markets for each party
        // Each sub-market has groupItemTitle like "Democrat" or "Republican"
        // And outcomes are ["Yes", "No"] with outcomePrices[0] being the Yes probability

        double demOdds = 0;
        double repOdds = 0;
        double totalVolume = 0;
        string? marketId = null;

        foreach (var subMarket in market.Markets)
        {
            var groupTitle = subMarket.GroupItemTitle?.ToLowerInvariant() ?? "";

            // Parse the Yes price (first outcome price)
            double yesPrice = 0;
            var outcomePrices = subMarket.GetOutcomePrices();
            if (outcomePrices.Count > 0)
            {
                double.TryParse(outcomePrices[0], out yesPrice);
            }

            // Sum up volume
            if (double.TryParse(subMarket.Volume, out var vol))
            {
                totalVolume += vol;
            }

            // Match by groupItemTitle
            if (groupTitle.Contains("democrat"))
            {
                demOdds = yesPrice;
                marketId ??= subMarket.ConditionId;
                _logger.LogDebug("Found Democrat market: Yes price = {Price}", yesPrice);
            }
            else if (groupTitle.Contains("republican"))
            {
                repOdds = yesPrice;
                _logger.LogDebug("Found Republican market: Yes price = {Price}", yesPrice);
            }
        }

        // If we didn't find party-specific markets, fall back to parsing outcomes
        if (demOdds == 0 && repOdds == 0)
        {
            var mainMarket = market.Markets
                .OrderByDescending(m => double.TryParse(m.Volume, out var v) ? v : 0)
                .FirstOrDefault();

            if (mainMarket != null)
            {
                var outcomes = mainMarket.GetOutcomes();
                var prices = mainMarket.GetOutcomePrices();
                if (prices.Count >= 2)
                {
                    for (int i = 0; i < Math.Min(outcomes.Count, prices.Count); i++)
                    {
                        var outcome = outcomes[i].ToLowerInvariant();
                        if (double.TryParse(prices[i], out var price))
                        {
                            if (outcome.Contains("democrat"))
                                demOdds = price;
                            else if (outcome.Contains("republican"))
                                repOdds = price;
                        }
                    }
                    marketId = mainMarket.ConditionId;
                }
            }
        }

        if (demOdds == 0 && repOdds == 0)
        {
            _logger.LogWarning("Could not parse odds for {RaceId} from market {Title}", raceId, market.Title);
            return null;
        }

        // Normalize odds to sum to 1
        var total = demOdds + repOdds;
        if (total > 0)
        {
            demOdds /= total;
            repOdds /= total;
        }

        _logger.LogInformation("Parsed Polymarket {RaceId}: Dem={DemOdds:P1}, Rep={RepOdds:P1}, Volume=${Volume:N0}",
            raceId, demOdds, repOdds, totalVolume);

        return new MarketOdds
        {
            RaceId = raceId,
            Source = SourceName,
            DemOdds = demOdds,
            RepOdds = repOdds,
            Timestamp = DateTime.UtcNow,
            Volume = totalVolume > 0 ? totalVolume : null,
            ExternalMarketId = marketId ?? market.Id
        };
    }

    private string? MapMarketToRaceId(PolymarketEvent market)
    {
        if (string.IsNullOrEmpty(market.Title))
            return null;

        var title = market.Title;
        var titleLower = title.ToLowerInvariant();

        // Polymarket title patterns:
        // - Senate: "{State} Senate election winner" (e.g., "North Carolina Senate election winner")
        // - House: "{ST}-{##} House Election Winner" (e.g., "NC-01 House Election Winner")
        // - Governor: "{State} Governor Election Winner" (e.g., "North Carolina Governor Election Winner")

        // Check for House race first (uses state abbreviation + district format)
        if (titleLower.Contains("house election winner"))
        {
            // Extract "NC-01" pattern from title
            var match = System.Text.RegularExpressions.Regex.Match(title, @"([A-Z]{2})-(\d{2})\s+House", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var houseStateId = match.Groups[1].Value.ToUpperInvariant();
                var district = int.Parse(match.Groups[2].Value);
                return $"{houseStateId}-{district:D2}-2026";
            }
            return null;
        }

        // For Senate and Governor, extract state name
        var stateNameToId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Alabama", "AL" }, { "Alaska", "AK" }, { "Arizona", "AZ" }, { "Arkansas", "AR" },
            { "California", "CA" }, { "Colorado", "CO" }, { "Connecticut", "CT" }, { "Delaware", "DE" },
            { "Florida", "FL" }, { "Georgia", "GA" }, { "Hawaii", "HI" }, { "Idaho", "ID" },
            { "Illinois", "IL" }, { "Indiana", "IN" }, { "Iowa", "IA" }, { "Kansas", "KS" },
            { "Kentucky", "KY" }, { "Louisiana", "LA" }, { "Maine", "ME" }, { "Maryland", "MD" },
            { "Massachusetts", "MA" }, { "Michigan", "MI" }, { "Minnesota", "MN" }, { "Mississippi", "MS" },
            { "Missouri", "MO" }, { "Montana", "MT" }, { "Nebraska", "NE" }, { "Nevada", "NV" },
            { "New Hampshire", "NH" }, { "New Jersey", "NJ" }, { "New Mexico", "NM" }, { "New York", "NY" },
            { "North Carolina", "NC" }, { "North Dakota", "ND" }, { "Ohio", "OH" }, { "Oklahoma", "OK" },
            { "Oregon", "OR" }, { "Pennsylvania", "PA" }, { "Rhode Island", "RI" }, { "South Carolina", "SC" },
            { "South Dakota", "SD" }, { "Tennessee", "TN" }, { "Texas", "TX" }, { "Utah", "UT" },
            { "Vermont", "VT" }, { "Virginia", "VA" }, { "Washington", "WA" }, { "West Virginia", "WV" },
            { "Wisconsin", "WI" }, { "Wyoming", "WY" }
        };

        string? stateId = null;
        foreach (var (name, id) in stateNameToId)
        {
            if (title.Contains(name, StringComparison.OrdinalIgnoreCase))
            {
                stateId = id;
                break;
            }
        }

        if (stateId == null)
        {
            _logger.LogDebug("Could not extract state from market title: {Title}", title);
            return null;
        }

        // Determine race type
        string raceType;
        if (titleLower.Contains("senate election winner"))
            raceType = "SEN";
        else if (titleLower.Contains("governor election winner"))
            raceType = "GOV";
        else
        {
            _logger.LogDebug("Could not determine race type from market title: {Title}", title);
            return null;
        }

        var raceId = $"{stateId}-{raceType}-2026";
        _logger.LogDebug("Mapped market '{Title}' to race ID: {RaceId}", title, raceId);
        return raceId;
    }

    private async Task SaveOddsToDbAsync(MarketOdds odds, CancellationToken cancellationToken)
    {
        var entity = new MarketOddsEntity
        {
            RaceId = odds.RaceId,
            Source = odds.Source,
            Timestamp = odds.Timestamp,
            DemOdds = odds.DemOdds,
            RepOdds = odds.RepOdds,
            Volume = odds.Volume,
            ExternalMarketId = odds.ExternalMarketId
        };

        _dbContext.MarketOdds.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static MarketOdds EntityToModel(MarketOddsEntity entity)
    {
        return new MarketOdds
        {
            RaceId = entity.RaceId,
            Source = entity.Source,
            DemOdds = entity.DemOdds,
            RepOdds = entity.RepOdds,
            Timestamp = entity.Timestamp,
            Volume = entity.Volume,
            ExternalMarketId = entity.ExternalMarketId
        };
    }

    // DTOs for Polymarket API responses
    private class PolymarketEvent
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? Slug { get; set; }
        public string? Description { get; set; }
        public List<PolymarketMarket>? Markets { get; set; }
    }

    private class PolymarketMarket
    {
        public string? Id { get; set; }
        public string? Question { get; set; }
        public string? ConditionId { get; set; }
        public string? Volume { get; set; }
        // These are JSON-encoded strings like "[\"Yes\", \"No\"]"
        public string? Outcomes { get; set; }
        public string? OutcomePrices { get; set; }
        public string? GroupItemTitle { get; set; }

        public List<string> GetOutcomes()
        {
            if (string.IsNullOrEmpty(Outcomes)) return new List<string>();
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<string>>(Outcomes) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        public List<string> GetOutcomePrices()
        {
            if (string.IsNullOrEmpty(OutcomePrices)) return new List<string>();
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<string>>(OutcomePrices) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }
    }
}
