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
/// Client for fetching prediction market data from Polymarket using direct market IDs.
/// Market IDs can be found by visiting polymarket.com/event/{slug} and checking the network tab.
/// </summary>
public class PolymarketClient : IPredictionMarketSource
{
    private readonly HttpClient _httpClient;
    private readonly ForecastDbContext _dbContext;
    private readonly ILogger<PolymarketClient> _logger;
    private readonly Dictionary<string, MarketOdds> _cachedOdds = new();
    private DateTime _lastRefresh = DateTime.MinValue;

    // Polymarket API endpoint for fetching market data by ID
    private const string MarketsApiBaseUrl = "https://gamma-api.polymarket.com/markets";

    // Mapping of race IDs to Polymarket market IDs
    // These IDs are found by visiting the market page and checking the network tab
    // Example: polymarket.com/event/texas-senate-election-winner -> API call to /markets/630964
    private static readonly Dictionary<string, string> RaceIdToMarketId = new()
    {
        // Senate races 2026
        { "TX-SEN-2026", "630964" },   
        { "OR-SEN-2026", "630898" },  
        { "GA-SEN-2026", "630692" },  
        { "NC-SEN-2026", "630883" },   
        { "MI-SEN-2026", "630805" },   
        { "MO-SEN-2026", "630832" },    
        { "ID-SEN-2026", "630706" },    
        { "WY-SEN-2026", "631003" },    
        { "NM-SEN-2026", "630870" },    
        { "AK-SEN-2026", "634974" },    
        { "CO-SEN-2026", "630666" },    
        { "SD-SEN-2026", "630938" },   
        { "NE-SEN-2026", "634892" },  // Not just dem/rep  
        { "KS-SEN-2026", "630747" },    
        { "LA-SEN-2026", "634879" },
        { "IL-SEN-2026", "630720" }, 
        { "OK-SEN-2026", "631031" },  
        { "MN-SEN-2026", "630818" },
        { "IA-SEN-2026", "630734" },     
        { "AR-SEN-2026", "630654" },   
        { "MS-SEN-2026", "631018" },     
        { "KY-SEN-2026", "630760" },    
        { "TN-SEN-2026", "630951" },     
        { "AL-SEN-2026", "630628" },   
        { "SC-SEN-2026", "630925" },     
        { "VA-SEN-2026", "630976" },  
        { "WV-SEN-2026", "630990" },     
        { "DE-SEN-2026", "630679" },
        { "NJ-SEN-2026", "630857" },     
        { "RI-SEN-2026", "630911" }, 
        { "MA-SEN-2026", "630790" },     
        { "ME-SEN-2026", "630772" },     
        { "NH-SEN-2026", "630844" },     

        // Governor races 2026
        { "GA-GOV-2026", "629337" },    
        { "PA-GOV-2026", "629614" },    
        { "MI-GOV-2026", "629464" }, // Not just dem/rep  
        { "WI-GOV-2026", "629720" },    
        { "AZ-GOV-2026", "629183" },    
        { "NV-GOV-2026", "629505" },    
        { "CA-GOV-2026", "628954" }, // Not just dem/rep
        { "FL-GOV-2026", "629325" },    
        { "OR-GOV-2026", "629583" },   
        { "OH-GOV-2026", "629558" },
        { "ID-GOV-2026", "629364" },
        { "WY-GOV-2026", "629734" },
        { "CO-GOV-2026", "629296" },
        { "NM-GOV-2026", "629531" },
        { "AK-GOV-2026", "635012" }, // Not just dem/rep
        { "HI-GOV-2026", "629350" }, 
        { "OK-GOV-2026", "629571" },
        { "KS-GOV-2026", "629413" },
        { "NE-GOV-2026", "629491" },
        { "SD-GOV-2026", "629654" },
        { "MN-GOV-2026", "629477" },
        { "IA-GOV-2026", "629397" },
        { "AR-GOV-2026", "629284" },
        { "AL-GOV-2026", "629271" },
        { "SC-GOV-2026", "629641" },
        { "TN-GOV-2026", "629667" },
        { "IL-GOV-2026", "629376" },
        { "NY-GOV-2026", "629544" },
        { "ME-GOV-2026", "629425" },
        { "NH-GOV-2026", "629519" },
        { "VT-GOV-2026", "629708" },
        { "MA-GOV-2026", "629451" },
        { "CT-GOV-2026", "629311" },
        { "RI-GOV-2026", "629627" },
        { "MD-GOV-2026", "629438" },
        { "TX-GOV-2026", "629691" }
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
        _logger.LogDebug("PolymarketClient.GetRaceOddsAsync called for {RaceId}", raceId);

        // Check if we have a market ID mapping for this race
        if (!RaceIdToMarketId.TryGetValue(raceId, out var marketId))
        {
            _logger.LogDebug("No Polymarket market ID mapping for {RaceId}", raceId);
            return null;
        }

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

        // Fetch fresh data from Polymarket
        _logger.LogInformation("Fetching fresh odds from Polymarket for {RaceId} (market ID: {MarketId})", raceId, marketId);
        var freshOdds = await FetchMarketOddsByIdAsync(raceId, marketId, cancellationToken);

        if (freshOdds != null)
        {
            _logger.LogInformation("Got fresh odds for {RaceId}: Dem={Dem:P1}, Rep={Rep:P1}",
                raceId, freshOdds.DemOdds, freshOdds.RepOdds);
            _cachedOdds[raceId] = freshOdds;
            await SaveOddsToDbAsync(freshOdds, cancellationToken);
            return freshOdds;
        }

        _logger.LogWarning("Could not get odds for {RaceId} from market {MarketId}", raceId, marketId);

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
        _logger.LogInformation("Refreshing Polymarket data for {Count} configured markets...", RaceIdToMarketId.Count);

        var successCount = 0;
        foreach (var (raceId, marketId) in RaceIdToMarketId)
        {
            try
            {
                var odds = await FetchMarketOddsByIdAsync(raceId, marketId, cancellationToken);
                if (odds != null)
                {
                    _cachedOdds[raceId] = odds;
                    await SaveOddsToDbAsync(odds, cancellationToken);
                    successCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error fetching market {MarketId} for {RaceId}", marketId, raceId);
            }
        }

        _lastRefresh = DateTime.UtcNow;
        _logger.LogInformation("Polymarket refresh complete. {Success}/{Total} markets updated.",
            successCount, RaceIdToMarketId.Count);
    }

    private async Task<MarketOdds?> FetchMarketOddsByIdAsync(string raceId, string marketId, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"{MarketsApiBaseUrl}/{marketId}";
            _logger.LogDebug("Fetching Polymarket data from {Url}", url);

            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Polymarket API returned {Status} for market {MarketId}",
                    response.StatusCode, marketId);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var market = JsonSerializer.Deserialize<PolymarketMarketResponse>(
                content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (market == null)
            {
                _logger.LogWarning("Failed to parse Polymarket response for market {MarketId}", marketId);
                return null;
            }

            return ParseMarketResponse(market, raceId, marketId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching market {MarketId} for {RaceId}", marketId, raceId);
            return null;
        }
    }

    private MarketOdds? ParseMarketResponse(PolymarketMarketResponse market, string raceId, string marketId)
    {
        // The market response contains outcomePrices as a JSON array string like "[\"0.45\", \"0.55\"]"
        // and outcomes like "[\"Democrat\", \"Republican\"]" or "[\"Yes\", \"No\"]"

        var outcomes = market.GetOutcomes();
        var prices = market.GetOutcomePrices();

        if (outcomes.Count < 2 || prices.Count < 2)
        {
            _logger.LogWarning("Market {MarketId} has insufficient outcomes/prices", marketId);
            return null;
        }

        double demOdds = 0;
        double repOdds = 0;

        // Try to match outcomes to parties
        for (int i = 0; i < Math.Min(outcomes.Count, prices.Count); i++)
        {
            var outcome = outcomes[i].ToLowerInvariant();
            if (double.TryParse(prices[i], out var price))
            {
                if (outcome.Contains("democrat") || outcome.Contains("dem") || outcome == "yes")
                {
                    // For "Yes/No" markets, check the question to determine party
                    if (outcome == "yes" && market.Question != null)
                    {
                        if (market.Question.ToLowerInvariant().Contains("democrat"))
                            demOdds = price;
                        else if (market.Question.ToLowerInvariant().Contains("republican"))
                            repOdds = price;
                        else
                            demOdds = price; // Default assumption
                    }
                    else
                    {
                        demOdds = price;
                    }
                }
                else if (outcome.Contains("republican") || outcome.Contains("rep") || outcome.Contains("gop"))
                {
                    repOdds = price;
                }
            }
        }

        // If we only found one party and it's a two-outcome market, calculate the other
        if (demOdds > 0 && repOdds == 0 && outcomes.Count == 2)
        {
            repOdds = 1.0 - demOdds;
        }
        else if (repOdds > 0 && demOdds == 0 && outcomes.Count == 2)
        {
            demOdds = 1.0 - repOdds;
        }

        if (demOdds == 0 && repOdds == 0)
        {
            _logger.LogWarning("Could not parse party odds from market {MarketId}. Outcomes: {Outcomes}",
                marketId, string.Join(", ", outcomes));
            return null;
        }

        // Normalize odds to sum to 1
        var total = demOdds + repOdds;
        if (total > 0 && Math.Abs(total - 1.0) > 0.01)
        {
            demOdds /= total;
            repOdds /= total;
        }

        // Parse volume
        double? volume = null;
        if (double.TryParse(market.Volume, out var vol))
        {
            volume = vol;
        }

        _logger.LogInformation("Parsed Polymarket {RaceId}: Dem={DemOdds:P1}, Rep={RepOdds:P1}, Volume=${Volume:N0}",
            raceId, demOdds, repOdds, volume ?? 0);

        return new MarketOdds
        {
            RaceId = raceId,
            Source = SourceName,
            DemOdds = demOdds,
            RepOdds = repOdds,
            Timestamp = DateTime.UtcNow,
            Volume = volume,
            ExternalMarketId = marketId
        };
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

    // Chamber control market IDs (for overall Senate/House control)
    private static readonly Dictionary<string, string> ChamberMarketIds = new()
    {
        { "Senate", "562794" },  // Which party will control the Senate?
        // Add House market ID here when available
    };

    /// <summary>
    /// Gets the overall chamber control odds from Polymarket.
    /// </summary>
    public async Task<MarketOdds?> GetChamberOddsAsync(string chamber, CancellationToken cancellationToken = default)
    {
        if (!ChamberMarketIds.TryGetValue(chamber, out var marketId))
        {
            _logger.LogDebug("No Polymarket market ID for chamber {Chamber}", chamber);
            return null;
        }

        var raceId = $"CHAMBER-{chamber.ToUpper()}";

        try
        {
            var url = $"{MarketsApiBaseUrl}/{marketId}";
            _logger.LogInformation("Fetching Polymarket chamber odds from {Url}", url);

            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Polymarket API returned {Status} for chamber market {MarketId}",
                    response.StatusCode, marketId);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var market = JsonSerializer.Deserialize<PolymarketMarketResponse>(
                content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (market == null)
            {
                _logger.LogWarning("Failed to parse Polymarket response for chamber market {MarketId}", marketId);
                return null;
            }

            return ParseMarketResponse(market, raceId, marketId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching chamber market {MarketId}", marketId);
            return null;
        }
    }

    /// <summary>
    /// Adds or updates a market ID mapping for a race.
    /// Call this to add new markets discovered from the Polymarket website.
    /// </summary>
    public static void AddMarketMapping(string raceId, string marketId)
    {
        RaceIdToMarketId[raceId] = marketId;
    }

    /// <summary>
    /// Gets all configured race IDs that have Polymarket mappings.
    /// </summary>
    public static IReadOnlyCollection<string> GetConfiguredRaceIds() => RaceIdToMarketId.Keys;

    // DTO for Polymarket /markets/{id} API response
    private class PolymarketMarketResponse
    {
        public string? Id { get; set; }
        public string? Question { get; set; }
        public string? ConditionId { get; set; }
        public string? Volume { get; set; }
        public string? Outcomes { get; set; }
        public string? OutcomePrices { get; set; }
        public string? GroupItemTitle { get; set; }
        public bool? Active { get; set; }
        public bool? Closed { get; set; }

        public List<string> GetOutcomes()
        {
            if (string.IsNullOrEmpty(Outcomes)) return new List<string>();
            try
            {
                return JsonSerializer.Deserialize<List<string>>(Outcomes) ?? new List<string>();
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
                return JsonSerializer.Deserialize<List<string>>(OutcomePrices) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }
    }
}
