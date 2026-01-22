using System.Globalization;
using ElectionForecaster.Infrastructure.Data;
using ElectionForecaster.Infrastructure.Data.Entities;
using ElectionForecaster.Infrastructure.DataSources.Interfaces;
using ElectionForecaster.Infrastructure.DataSources.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ElectionForecaster.Infrastructure.DataSources.Polling;

/// <summary>
/// Client for fetching polling data from FiveThirtyEight's GitHub data repository.
/// </summary>
public class FiveThirtyEightClient : IPollingSource
{
    private readonly HttpClient _httpClient;
    private readonly ForecastDbContext _dbContext;
    private readonly ILogger<FiveThirtyEightClient> _logger;
    private readonly Dictionary<string, PollingAverage> _cachedAverages = new();
    private readonly List<PollData> _cachedPolls = new();
    private DateTime _lastRefresh = DateTime.MinValue;

    // FiveThirtyEight GitHub raw CSV URLs
    private const string PollsBaseUrl = "https://raw.githubusercontent.com/fivethirtyeight/data/master/polls";
    private const string SenatePolls = $"{PollsBaseUrl}/senate_polls.csv";
    private const string GovernorPolls = $"{PollsBaseUrl}/governor_polls.csv";
    private const string HousePolls = $"{PollsBaseUrl}/house_polls.csv";
    private const string GenericBallotPolls = $"{PollsBaseUrl}/generic_ballot_polls.csv";

    // Pollster ratings URL
    private const string PollsterRatingsUrl = "https://raw.githubusercontent.com/fivethirtyeight/data/master/pollster-ratings/pollster-ratings.csv";

    public string SourceName => "FiveThirtyEight";

    private Dictionary<string, string> _pollsterRatings = new();

    public FiveThirtyEightClient(
        HttpClient httpClient,
        ForecastDbContext dbContext,
        ILogger<FiveThirtyEightClient> logger)
    {
        _httpClient = httpClient;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<PollingAverage?> GetPollingAverageAsync(string raceId, CancellationToken cancellationToken = default)
    {
        // Check cache first
        if (_cachedAverages.TryGetValue(raceId, out var cached) &&
            cached.LatestPollDate.HasValue &&
            (DateTime.UtcNow - _lastRefresh).TotalHours < 6)
        {
            return cached;
        }

        // Calculate average from recent polls
        var polls = await GetRecentPollsAsync(raceId, 60, cancellationToken);
        if (polls.Count == 0)
        {
            return null;
        }

        var average = CalculateWeightedAverage(polls, raceId);
        _cachedAverages[raceId] = average;
        return average;
    }

    public async Task<List<PollData>> GetRecentPollsAsync(string raceId, int days = 30, CancellationToken cancellationToken = default)
    {
        // Check if we have fresh cached data
        if (_cachedPolls.Count > 0 && (DateTime.UtcNow - _lastRefresh).TotalHours < 6)
        {
            return _cachedPolls
                .Where(p => p.RaceId == raceId && p.Date >= DateTime.UtcNow.AddDays(-days))
                .OrderByDescending(p => p.Date)
                .ToList();
        }

        // Check database
        var cutoffDate = DateTime.UtcNow.AddDays(-days);
        var dbPolls = await _dbContext.Polls
            .Where(p => p.RaceId == raceId && p.Date >= cutoffDate)
            .OrderByDescending(p => p.Date)
            .ToListAsync(cancellationToken);

        if (dbPolls.Count > 0)
        {
            return dbPolls.Select(EntityToModel).ToList();
        }

        // Fetch fresh data if needed
        await RefreshAsync(cancellationToken);

        return _cachedPolls
            .Where(p => p.RaceId == raceId && p.Date >= cutoffDate)
            .OrderByDescending(p => p.Date)
            .ToList();
    }

    public async Task<Dictionary<string, PollingAverage>> GetAllPollingAveragesAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedAverages.Count > 0 && (DateTime.UtcNow - _lastRefresh).TotalHours < 6)
        {
            return new Dictionary<string, PollingAverage>(_cachedAverages);
        }

        await RefreshAsync(cancellationToken);

        // Calculate averages for each race
        var raceIds = _cachedPolls.Select(p => p.RaceId).Distinct();
        foreach (var raceId in raceIds)
        {
            var polls = _cachedPolls.Where(p => p.RaceId == raceId).ToList();
            if (polls.Count > 0)
            {
                _cachedAverages[raceId] = CalculateWeightedAverage(polls, raceId);
            }
        }

        return new Dictionary<string, PollingAverage>(_cachedAverages);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Refreshing FiveThirtyEight polling data...");

        try
        {
            // Load pollster ratings first
            await LoadPollsterRatingsAsync(cancellationToken);

            // Fetch all poll types in parallel
            var tasks = new[]
            {
                FetchPollsCsvAsync(SenatePolls, "Senate", cancellationToken),
                FetchPollsCsvAsync(GovernorPolls, "Governor", cancellationToken),
                FetchPollsCsvAsync(HousePolls, "House", cancellationToken)
            };

            var results = await Task.WhenAll(tasks);
            var allPolls = results.SelectMany(r => r).ToList();

            _cachedPolls.Clear();
            _cachedPolls.AddRange(allPolls);

            // Save to database
            await SavePollsToDbAsync(allPolls, cancellationToken);

            _lastRefresh = DateTime.UtcNow;
            _logger.LogInformation("FiveThirtyEight refresh complete. {Count} polls loaded.", allPolls.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing FiveThirtyEight data");
        }
    }

    private async Task LoadPollsterRatingsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var csv = await _httpClient.GetStringAsync(PollsterRatingsUrl, cancellationToken);
            var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length <= 1) return;

            var headers = ParseCsvLine(lines[0]);
            var pollsterIndex = Array.IndexOf(headers, "pollster");
            var ratingIndex = Array.IndexOf(headers, "538_grade");

            if (pollsterIndex < 0) pollsterIndex = Array.IndexOf(headers, "Pollster");
            if (ratingIndex < 0) ratingIndex = Array.IndexOf(headers, "538 Grade");

            if (pollsterIndex < 0 || ratingIndex < 0) return;

            _pollsterRatings.Clear();
            for (int i = 1; i < lines.Length; i++)
            {
                var values = ParseCsvLine(lines[i]);
                if (values.Length > Math.Max(pollsterIndex, ratingIndex))
                {
                    var pollster = values[pollsterIndex].Trim();
                    var rating = values[ratingIndex].Trim();
                    if (!string.IsNullOrEmpty(pollster) && !string.IsNullOrEmpty(rating))
                    {
                        _pollsterRatings[pollster.ToLowerInvariant()] = rating;
                    }
                }
            }

            _logger.LogDebug("Loaded {Count} pollster ratings", _pollsterRatings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load pollster ratings");
        }
    }

    private async Task<List<PollData>> FetchPollsCsvAsync(string url, string raceType, CancellationToken cancellationToken)
    {
        var polls = new List<PollData>();

        try
        {
            var csv = await _httpClient.GetStringAsync(url, cancellationToken);
            var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length <= 1) return polls;

            var headers = ParseCsvLine(lines[0]);
            var columnMap = BuildColumnMap(headers);

            for (int i = 1; i < lines.Length; i++)
            {
                try
                {
                    var poll = ParsePollLine(lines[i], columnMap, raceType);
                    if (poll != null && poll.Date >= DateTime.UtcNow.AddDays(-180)) // Only keep recent polls
                    {
                        polls.Add(poll);
                    }
                }
                catch
                {
                    // Skip malformed lines
                }
            }

            _logger.LogDebug("Parsed {Count} {RaceType} polls", polls.Count, raceType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching {RaceType} polls from {Url}", raceType, url);
        }

        return polls;
    }

    private Dictionary<string, int> BuildColumnMap(string[] headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Length; i++)
        {
            map[headers[i].Trim()] = i;
        }
        return map;
    }

    private PollData? ParsePollLine(string line, Dictionary<string, int> columns, string raceType)
    {
        var values = ParseCsvLine(line);

        // Get state
        string? state = GetValue(values, columns, "state");
        if (string.IsNullOrEmpty(state)) return null;

        // Get district number for House races
        int? district = null;
        if (raceType == "House")
        {
            var districtStr = GetValue(values, columns, "seat_number") ??
                             GetValue(values, columns, "district");
            if (int.TryParse(districtStr, out var d))
                district = d;
        }

        // Build race ID
        var stateAbbr = GetStateAbbreviation(state);
        if (stateAbbr == null) return null;

        var raceTypeCode = raceType switch
        {
            "Senate" => "SEN",
            "Governor" => "GOV",
            "House" => $"HOUSE-{district:D2}",
            _ => raceType
        };

        var raceId = $"{stateAbbr}-{raceTypeCode}-2026";

        // Parse date
        var dateStr = GetValue(values, columns, "end_date") ??
                     GetValue(values, columns, "created_at") ??
                     GetValue(values, columns, "poll_date");
        if (!DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return null;

        // Get candidate results - find Dem and Rep percentages
        double demPct = 0, repPct = 0;

        // FiveThirtyEight format varies - try different column patterns
        var pct = GetValue(values, columns, "pct");
        var party = GetValue(values, columns, "party");
        var answer = GetValue(values, columns, "answer");

        if (!string.IsNullOrEmpty(pct) && double.TryParse(pct, out var pctValue))
        {
            if (party?.Equals("DEM", StringComparison.OrdinalIgnoreCase) == true ||
                answer?.Contains("Democrat", StringComparison.OrdinalIgnoreCase) == true)
            {
                demPct = pctValue;
            }
            else if (party?.Equals("REP", StringComparison.OrdinalIgnoreCase) == true ||
                     answer?.Contains("Republican", StringComparison.OrdinalIgnoreCase) == true)
            {
                repPct = pctValue;
            }
        }

        // Try alternate format with dem/rep columns
        if (demPct == 0)
        {
            var demStr = GetValue(values, columns, "dem") ?? GetValue(values, columns, "democrat");
            if (double.TryParse(demStr, out var d)) demPct = d;
        }
        if (repPct == 0)
        {
            var repStr = GetValue(values, columns, "rep") ?? GetValue(values, columns, "republican");
            if (double.TryParse(repStr, out var repVal)) repPct = repVal;
        }

        if (demPct == 0 && repPct == 0) return null;

        // Get pollster info
        var pollster = GetValue(values, columns, "pollster") ??
                      GetValue(values, columns, "pollster_name") ?? "Unknown";

        var sampleSizeStr = GetValue(values, columns, "sample_size") ??
                           GetValue(values, columns, "n");
        int? sampleSize = int.TryParse(sampleSizeStr, out var ss) ? ss : null;

        var population = GetValue(values, columns, "population") ??
                        GetValue(values, columns, "population_full");

        var methodology = GetValue(values, columns, "methodology");

        // Get pollster rating
        string? rating = null;
        if (_pollsterRatings.TryGetValue(pollster.ToLowerInvariant(), out var r))
        {
            rating = r;
        }

        return new PollData
        {
            RaceId = raceId,
            Pollster = pollster,
            Date = date,
            SampleSize = sampleSize,
            DemPercent = demPct,
            RepPercent = repPct,
            PollsterRating = rating,
            Methodology = methodology,
            Population = NormalizePopulation(population)
        };
    }

    private string? GetValue(string[] values, Dictionary<string, int> columns, string columnName)
    {
        if (columns.TryGetValue(columnName, out var index) && index < values.Length)
        {
            var value = values[index].Trim().Trim('"');
            return string.IsNullOrEmpty(value) ? null : value;
        }
        return null;
    }

    private static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = "";
        var inQuotes = false;

        foreach (var c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current);
                current = "";
            }
            else
            {
                current += c;
            }
        }
        result.Add(current);

        return result.ToArray();
    }

    private PollingAverage CalculateWeightedAverage(List<PollData> polls, string raceId)
    {
        if (polls.Count == 0)
        {
            return new PollingAverage { RaceId = raceId };
        }

        var now = DateTime.UtcNow;
        double totalWeight = 0;
        double weightedDem = 0;
        double weightedRep = 0;
        int totalSampleSize = 0;
        int sampleCount = 0;

        foreach (var poll in polls)
        {
            var weight = poll.GetWeight(now);
            totalWeight += weight;
            weightedDem += poll.DemPercent * weight;
            weightedRep += poll.RepPercent * weight;

            if (poll.SampleSize.HasValue)
            {
                totalSampleSize += poll.SampleSize.Value;
                sampleCount++;
            }
        }

        if (totalWeight == 0)
        {
            return new PollingAverage { RaceId = raceId };
        }

        var average = new PollingAverage
        {
            RaceId = raceId,
            DemPercent = weightedDem / totalWeight,
            RepPercent = weightedRep / totalWeight,
            PollCount = polls.Count,
            LatestPollDate = polls.Max(p => p.Date),
            AverageSampleSize = sampleCount > 0 ? totalSampleSize / sampleCount : null,
            Confidence = CalculateConfidence(polls, totalWeight)
        };

        return average;
    }

    private double CalculateConfidence(List<PollData> polls, double totalWeight)
    {
        // Confidence based on:
        // 1. Number of polls (more = better)
        // 2. Poll quality/ratings
        // 3. Recency of polls
        // 4. Consistency of results

        double confidence = 0.5;

        // Poll count bonus (up to +0.2)
        confidence += Math.Min(0.2, polls.Count * 0.02);

        // Recency bonus (up to +0.15)
        var latestPoll = polls.Max(p => p.Date);
        var daysSinceLatest = (DateTime.UtcNow - latestPoll).TotalDays;
        confidence += Math.Max(0, 0.15 - (daysSinceLatest * 0.01));

        // Quality bonus based on ratings (up to +0.15)
        var ratedPolls = polls.Where(p => !string.IsNullOrEmpty(p.PollsterRating)).ToList();
        if (ratedPolls.Count > 0)
        {
            var avgRatingScore = ratedPolls.Average(p => GetRatingScore(p.PollsterRating));
            confidence += avgRatingScore * 0.15;
        }

        return Math.Min(1.0, Math.Max(0.3, confidence));
    }

    private double GetRatingScore(string? rating)
    {
        return rating switch
        {
            "A+" => 1.0,
            "A" => 0.95,
            "A-" => 0.9,
            "A/B" => 0.85,
            "B+" => 0.8,
            "B" => 0.75,
            "B-" => 0.7,
            "B/C" => 0.65,
            "C+" => 0.6,
            "C" => 0.55,
            "C-" => 0.5,
            "C/D" => 0.45,
            "D+" => 0.4,
            "D" => 0.35,
            "D-" => 0.3,
            _ => 0.5
        };
    }

    private string? NormalizePopulation(string? population)
    {
        if (string.IsNullOrEmpty(population)) return null;

        var lower = population.ToLowerInvariant();
        if (lower.Contains("likely") || lower == "lv")
            return "LV";
        if (lower.Contains("registered") || lower == "rv")
            return "RV";
        if (lower.Contains("adult") || lower == "a")
            return "A";

        return population;
    }

    private string? GetStateAbbreviation(string state)
    {
        var stateMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
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

        // If already an abbreviation
        if (state.Length == 2 && stateMap.Values.Contains(state.ToUpperInvariant()))
            return state.ToUpperInvariant();

        if (stateMap.TryGetValue(state, out var abbr))
            return abbr;

        return null;
    }

    private async Task SavePollsToDbAsync(List<PollData> polls, CancellationToken cancellationToken)
    {
        foreach (var poll in polls)
        {
            // Check if poll already exists (by pollster, race, date combo)
            var exists = await _dbContext.Polls.AnyAsync(p =>
                p.RaceId == poll.RaceId &&
                p.Pollster == poll.Pollster &&
                p.Date.Date == poll.Date.Date,
                cancellationToken);

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

    private static PollData EntityToModel(PollEntity entity)
    {
        return new PollData
        {
            RaceId = entity.RaceId,
            Pollster = entity.Pollster,
            Date = entity.Date,
            SampleSize = entity.SampleSize,
            DemPercent = entity.DemPercent,
            RepPercent = entity.RepPercent,
            PollsterRating = entity.PollsterRating,
            Methodology = entity.Methodology,
            Population = entity.Population
        };
    }
}
