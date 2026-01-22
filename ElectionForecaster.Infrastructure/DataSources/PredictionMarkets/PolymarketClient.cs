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

    // Mapping of race IDs to Polymarket market condition IDs (slugs)
    // This mapping needs to be maintained as new markets are created
    private static readonly Dictionary<string, string> RaceToMarketSlug = new()
    {
        // 2026 Senate races - these would need to be updated when markets are created
        // Format: "StateId-RaceType-Year" -> "polymarket-slug"
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
        // First check cache
        if (_cachedOdds.TryGetValue(raceId, out var cached) &&
            (DateTime.UtcNow - cached.Timestamp).TotalMinutes < 15)
        {
            return cached;
        }

        // Check database for recent data
        var recentOdds = await _dbContext.MarketOdds
            .Where(m => m.RaceId == raceId && m.Source == SourceName)
            .OrderByDescending(m => m.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);

        if (recentOdds != null && (DateTime.UtcNow - recentOdds.Timestamp).TotalMinutes < 15)
        {
            var odds = EntityToModel(recentOdds);
            _cachedOdds[raceId] = odds;
            return odds;
        }

        // Try to fetch fresh data if we have a mapping
        if (RaceToMarketSlug.TryGetValue(raceId, out var slug))
        {
            var freshOdds = await FetchMarketOddsAsync(raceId, slug, cancellationToken);
            if (freshOdds != null)
            {
                _cachedOdds[raceId] = freshOdds;
                await SaveOddsToDbAsync(freshOdds, cancellationToken);
                return freshOdds;
            }
        }

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
            // Search for election-related events
            var searchTerms = new[] { "senate", "house", "governor", "congress", "election 2026" };

            foreach (var term in searchTerms)
            {
                var url = $"{GammaApiBaseUrl}/events?tag=politics&_limit=100&closed=false";

                var response = await _httpClient.GetAsync(url, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var events = await response.Content.ReadFromJsonAsync<List<PolymarketEvent>>(
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                        cancellationToken);

                    if (events != null)
                    {
                        results.AddRange(events.Where(e =>
                            e.Title?.Contains("2026", StringComparison.OrdinalIgnoreCase) == true ||
                            e.Title?.Contains("Senate", StringComparison.OrdinalIgnoreCase) == true ||
                            e.Title?.Contains("Governor", StringComparison.OrdinalIgnoreCase) == true));
                    }
                }

                // Avoid rate limiting
                await Task.Delay(100, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching election markets from Polymarket");
        }

        return results.DistinctBy(e => e.Id).ToList();
    }

    private async Task<MarketOdds?> FetchMarketOddsAsync(string raceId, string slug, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"{GammaApiBaseUrl}/events?slug={slug}";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch market {Slug}: {Status}", slug, response.StatusCode);
                return null;
            }

            var events = await response.Content.ReadFromJsonAsync<List<PolymarketEvent>>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);

            var eventData = events?.FirstOrDefault();
            if (eventData == null) return null;

            return ParseMarketToOdds(eventData, raceId);
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

        // Find the main market (usually the first one or the one with the most volume)
        var mainMarket = market.Markets
            .OrderByDescending(m => double.TryParse(m.Volume, out var v) ? v : 0)
            .FirstOrDefault();

        if (mainMarket == null) return null;

        // Parse outcomes to determine Dem/Rep odds
        double demOdds = 0.5;
        double repOdds = 0.5;
        double? volume = null;

        if (double.TryParse(mainMarket.Volume, out var vol))
            volume = vol;

        // Try to parse outcome prices
        if (mainMarket.OutcomePrices != null && mainMarket.OutcomePrices.Count >= 2)
        {
            // Polymarket typically has outcomes as [Yes, No] or [Dem, Rep]
            // We need to determine which is which based on the outcome names
            var outcomes = mainMarket.Outcomes ?? new List<string>();

            for (int i = 0; i < Math.Min(outcomes.Count, mainMarket.OutcomePrices.Count); i++)
            {
                var outcome = outcomes[i].ToLowerInvariant();
                if (double.TryParse(mainMarket.OutcomePrices[i], out var price))
                {
                    if (outcome.Contains("democrat") || outcome.Contains("dem") || outcome.Contains("yes"))
                    {
                        demOdds = price;
                    }
                    else if (outcome.Contains("republican") || outcome.Contains("rep") || outcome.Contains("no"))
                    {
                        repOdds = price;
                    }
                }
            }
        }

        // Normalize odds to sum to 1
        var total = demOdds + repOdds;
        if (total > 0)
        {
            demOdds /= total;
            repOdds /= total;
        }

        return new MarketOdds
        {
            RaceId = raceId,
            Source = SourceName,
            DemOdds = demOdds,
            RepOdds = repOdds,
            Timestamp = DateTime.UtcNow,
            Volume = volume,
            ExternalMarketId = mainMarket.ConditionId ?? market.Id
        };
    }

    private string? MapMarketToRaceId(PolymarketEvent market)
    {
        if (string.IsNullOrEmpty(market.Title))
            return null;

        var title = market.Title.ToLowerInvariant();

        // Try to extract state and race type from title
        // Common patterns: "Will Democrats win the 2026 Pennsylvania Senate race?"
        // "Pennsylvania Senate Election 2026"

        var states = new Dictionary<string, string>
        {
            { "alabama", "AL" }, { "alaska", "AK" }, { "arizona", "AZ" }, { "arkansas", "AR" },
            { "california", "CA" }, { "colorado", "CO" }, { "connecticut", "CT" }, { "delaware", "DE" },
            { "florida", "FL" }, { "georgia", "GA" }, { "hawaii", "HI" }, { "idaho", "ID" },
            { "illinois", "IL" }, { "indiana", "IN" }, { "iowa", "IA" }, { "kansas", "KS" },
            { "kentucky", "KY" }, { "louisiana", "LA" }, { "maine", "ME" }, { "maryland", "MD" },
            { "massachusetts", "MA" }, { "michigan", "MI" }, { "minnesota", "MN" }, { "mississippi", "MS" },
            { "missouri", "MO" }, { "montana", "MT" }, { "nebraska", "NE" }, { "nevada", "NV" },
            { "new hampshire", "NH" }, { "new jersey", "NJ" }, { "new mexico", "NM" }, { "new york", "NY" },
            { "north carolina", "NC" }, { "north dakota", "ND" }, { "ohio", "OH" }, { "oklahoma", "OK" },
            { "oregon", "OR" }, { "pennsylvania", "PA" }, { "rhode island", "RI" }, { "south carolina", "SC" },
            { "south dakota", "SD" }, { "tennessee", "TN" }, { "texas", "TX" }, { "utah", "UT" },
            { "vermont", "VT" }, { "virginia", "VA" }, { "washington", "WA" }, { "west virginia", "WV" },
            { "wisconsin", "WI" }, { "wyoming", "WY" }
        };

        string? stateId = null;
        foreach (var (name, id) in states)
        {
            if (title.Contains(name))
            {
                stateId = id;
                break;
            }
        }

        if (stateId == null) return null;

        string raceType;
        if (title.Contains("senate"))
            raceType = "SEN";
        else if (title.Contains("governor"))
            raceType = "GOV";
        else if (title.Contains("house") || title.Contains("congressional"))
            raceType = "HOUSE";
        else
            return null;

        // Format: PA-SEN-2026 or PA-GOV-2026
        return $"{stateId}-{raceType}-2026";
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
        public List<string>? Outcomes { get; set; }
        public List<string>? OutcomePrices { get; set; }
    }
}
