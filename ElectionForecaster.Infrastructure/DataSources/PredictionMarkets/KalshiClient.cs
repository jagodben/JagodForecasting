using System.Net.Http.Json;
using System.Text.Json;
using ElectionForecaster.Infrastructure.Data;
using ElectionForecaster.Infrastructure.Data.Entities;
using ElectionForecaster.Infrastructure.DataSources.Interfaces;
using ElectionForecaster.Infrastructure.DataSources.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ElectionForecaster.Infrastructure.DataSources.PredictionMarkets;

/// <summary>
/// Client for fetching prediction market data from Kalshi.
/// </summary>
public class KalshiClient : IPredictionMarketSource
{
    private readonly HttpClient _httpClient;
    private readonly ForecastDbContext _dbContext;
    private readonly ILogger<KalshiClient> _logger;
    private readonly Dictionary<string, MarketOdds> _cachedOdds = new();
    private DateTime _lastRefresh = DateTime.MinValue;

    // Kalshi API endpoints
    private const string ApiBaseUrl = "https://api.elections.kalshi.com/trade-api/v2";

    public string SourceName => "Kalshi";

    public KalshiClient(
        HttpClient httpClient,
        ForecastDbContext dbContext,
        ILogger<KalshiClient> logger)
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

        // Try to fetch fresh data
        var freshOdds = await FetchMarketOddsForRaceAsync(raceId, cancellationToken);
        if (freshOdds != null)
        {
            _cachedOdds[raceId] = freshOdds;
            await SaveOddsToDbAsync(freshOdds, cancellationToken);
            return freshOdds;
        }

        // Return cached data even if stale
        if (recentOdds != null)
        {
            return EntityToModel(recentOdds);
        }

        return null;
    }

    public async Task<Dictionary<string, MarketOdds>> GetAllRaceOddsAsync(CancellationToken cancellationToken = default)
    {
        if ((DateTime.UtcNow - _lastRefresh).TotalMinutes < 15 && _cachedOdds.Count > 0)
        {
            return new Dictionary<string, MarketOdds>(_cachedOdds);
        }

        await RefreshAsync(cancellationToken);
        return new Dictionary<string, MarketOdds>(_cachedOdds);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Refreshing Kalshi data...");

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
            _logger.LogInformation("Kalshi refresh complete. {Count} markets updated.", _cachedOdds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing Kalshi data");
        }
    }

    private async Task<List<KalshiMarket>> FetchElectionMarketsAsync(CancellationToken cancellationToken)
    {
        var results = new List<KalshiMarket>();

        try
        {
            // Kalshi organizes markets by series/events
            // Search for Senate, House, and Governor races
            var url = $"{ApiBaseUrl}/markets?status=open&series_ticker=SENATE,HOUSE,GOV&limit=200";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var apiResponse = await response.Content.ReadFromJsonAsync<KalshiMarketsResponse>(
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                    cancellationToken);

                if (apiResponse?.Markets != null)
                {
                    results.AddRange(apiResponse.Markets);
                }
            }
            else
            {
                _logger.LogWarning("Kalshi API returned {Status}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching election markets from Kalshi");
        }

        return results;
    }

    private async Task<MarketOdds?> FetchMarketOddsForRaceAsync(string raceId, CancellationToken cancellationToken)
    {
        try
        {
            // Convert race ID to Kalshi ticker format
            // e.g., PA-SEN-2026 -> SENATE-PA-26
            var ticker = ConvertToKalshiTicker(raceId);
            if (ticker == null) return null;

            var url = $"{ApiBaseUrl}/markets/{ticker}";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var apiResponse = await response.Content.ReadFromJsonAsync<KalshiMarketResponse>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);

            if (apiResponse?.Market == null) return null;

            return ParseMarketToOdds(apiResponse.Market, raceId);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error fetching Kalshi market for {RaceId}", raceId);
            return null;
        }
    }

    private MarketOdds? ParseMarketToOdds(KalshiMarket market, string raceId)
    {
        if (market == null) return null;

        // Kalshi uses yes/no prices where yes_bid represents the probability
        // of the outcome occurring
        double demOdds = 0.5;
        double repOdds = 0.5;

        // Determine if this is a Dem or Rep market based on the title
        bool isDemMarket = market.Title?.Contains("Democrat", StringComparison.OrdinalIgnoreCase) == true ||
                          market.YesSubTitle?.Contains("Democrat", StringComparison.OrdinalIgnoreCase) == true;

        if (market.YesBid.HasValue && market.NoBid.HasValue)
        {
            // Prices are in cents (0-100)
            var yesPrice = market.YesBid.Value / 100.0;
            var noPrice = market.NoBid.Value / 100.0;

            if (isDemMarket)
            {
                demOdds = yesPrice;
                repOdds = 1.0 - yesPrice;
            }
            else
            {
                repOdds = yesPrice;
                demOdds = 1.0 - yesPrice;
            }
        }
        else if (market.LastPrice.HasValue)
        {
            var price = market.LastPrice.Value / 100.0;
            if (isDemMarket)
            {
                demOdds = price;
                repOdds = 1.0 - price;
            }
            else
            {
                repOdds = price;
                demOdds = 1.0 - price;
            }
        }

        return new MarketOdds
        {
            RaceId = raceId,
            Source = SourceName,
            DemOdds = demOdds,
            RepOdds = repOdds,
            Timestamp = DateTime.UtcNow,
            Volume = market.Volume,
            ExternalMarketId = market.Ticker
        };
    }

    private string? MapMarketToRaceId(KalshiMarket market)
    {
        if (string.IsNullOrEmpty(market.Ticker))
            return null;

        // Kalshi ticker format examples: SENATE-PA-26-DEM, GOV-AZ-26-DEM
        var parts = market.Ticker.Split('-');
        if (parts.Length < 3) return null;

        var raceType = parts[0] switch
        {
            "SENATE" => "SEN",
            "GOV" => "GOV",
            "HOUSE" => "HOUSE",
            _ => null
        };

        if (raceType == null) return null;

        var stateId = parts[1];
        if (stateId.Length != 2) return null;

        // Assume 2026 for now
        return $"{stateId}-{raceType}-2026";
    }

    private string? ConvertToKalshiTicker(string raceId)
    {
        // Convert PA-SEN-2026 to SENATE-PA-26
        var parts = raceId.Split('-');
        if (parts.Length < 3) return null;

        var stateId = parts[0];
        var raceType = parts[1] switch
        {
            "SEN" => "SENATE",
            "GOV" => "GOV",
            "HOUSE" => "HOUSE",
            _ => null
        };

        if (raceType == null) return null;

        return $"{raceType}-{stateId}-26";
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

    // DTOs for Kalshi API
    private class KalshiMarketsResponse
    {
        public List<KalshiMarket>? Markets { get; set; }
        public string? Cursor { get; set; }
    }

    private class KalshiMarketResponse
    {
        public KalshiMarket? Market { get; set; }
    }

    private class KalshiMarket
    {
        public string? Ticker { get; set; }
        public string? Title { get; set; }
        public string? Subtitle { get; set; }
        public string? YesSubTitle { get; set; }
        public string? NoSubTitle { get; set; }
        public double? YesBid { get; set; }
        public double? YesAsk { get; set; }
        public double? NoBid { get; set; }
        public double? NoAsk { get; set; }
        public double? LastPrice { get; set; }
        public double? Volume { get; set; }
        public string? Status { get; set; }
    }
}
