using ElectionForecaster.Infrastructure.DataSources.Interfaces;
using ElectionForecaster.Infrastructure.DataSources.Models;
using Microsoft.Extensions.Logging;

namespace ElectionForecaster.Infrastructure.DataSources.Polling;

/// <summary>
/// Aggregates polling data from multiple sources into a unified average.
/// </summary>
public class PollingAggregator : IPollingSource
{
    private readonly IEnumerable<IPollingSource> _sources;
    private readonly ILogger<PollingAggregator> _logger;

    public string SourceName => "Aggregated";

    public PollingAggregator(
        IEnumerable<IPollingSource> sources,
        ILogger<PollingAggregator> logger)
    {
        // Filter out the aggregator itself to avoid circular reference
        _sources = sources.Where(s => s.SourceName != "Aggregated");
        _logger = logger;
    }

    public async Task<PollingAverage?> GetPollingAverageAsync(string raceId, CancellationToken cancellationToken = default)
    {
        var averages = new List<PollingAverage>();

        foreach (var source in _sources)
        {
            try
            {
                var avg = await source.GetPollingAverageAsync(raceId, cancellationToken);
                if (avg != null && avg.PollCount > 0)
                {
                    averages.Add(avg);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting polling average from {Source}", source.SourceName);
            }
        }

        if (averages.Count == 0)
            return null;

        return CombineAverages(averages, raceId);
    }

    public async Task<List<PollData>> GetRecentPollsAsync(string raceId, int days = 30, CancellationToken cancellationToken = default)
    {
        var allPolls = new List<PollData>();

        foreach (var source in _sources)
        {
            try
            {
                var polls = await source.GetRecentPollsAsync(raceId, days, cancellationToken);
                allPolls.AddRange(polls);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting polls from {Source}", source.SourceName);
            }
        }

        // Deduplicate by pollster + date
        return allPolls
            .GroupBy(p => new { p.Pollster, p.Date.Date })
            .Select(g => g.First())
            .OrderByDescending(p => p.Date)
            .ToList();
    }

    public async Task<Dictionary<string, PollingAverage>> GetAllPollingAveragesAsync(CancellationToken cancellationToken = default)
    {
        var allAverages = new Dictionary<string, List<PollingAverage>>();

        foreach (var source in _sources)
        {
            try
            {
                var sourceAverages = await source.GetAllPollingAveragesAsync(cancellationToken);
                foreach (var (raceId, avg) in sourceAverages)
                {
                    if (!allAverages.ContainsKey(raceId))
                        allAverages[raceId] = new List<PollingAverage>();

                    allAverages[raceId].Add(avg);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting all polling averages from {Source}", source.SourceName);
            }
        }

        var result = new Dictionary<string, PollingAverage>();
        foreach (var (raceId, averages) in allAverages)
        {
            if (averages.Count > 0)
            {
                result[raceId] = CombineAverages(averages, raceId);
            }
        }

        return result;
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var tasks = _sources.Select(s => s.RefreshAsync(cancellationToken));
        await Task.WhenAll(tasks);
    }

    private PollingAverage CombineAverages(List<PollingAverage> averages, string raceId)
    {
        if (averages.Count == 1)
            return averages[0];

        // Weight by poll count and confidence
        double totalWeight = 0;
        double weightedDem = 0;
        double weightedRep = 0;
        int totalPolls = 0;
        DateTime? latestPoll = null;

        foreach (var avg in averages)
        {
            // Weight by poll count and confidence
            double weight = avg.PollCount * avg.Confidence;
            totalWeight += weight;
            weightedDem += avg.DemPercent * weight;
            weightedRep += avg.RepPercent * weight;
            totalPolls += avg.PollCount;

            if (avg.LatestPollDate.HasValue)
            {
                if (!latestPoll.HasValue || avg.LatestPollDate > latestPoll)
                    latestPoll = avg.LatestPollDate;
            }
        }

        if (totalWeight == 0)
        {
            // Simple average if no weights
            return new PollingAverage
            {
                RaceId = raceId,
                DemPercent = averages.Average(a => a.DemPercent),
                RepPercent = averages.Average(a => a.RepPercent),
                PollCount = totalPolls,
                LatestPollDate = latestPoll,
                Confidence = averages.Average(a => a.Confidence)
            };
        }

        return new PollingAverage
        {
            RaceId = raceId,
            DemPercent = weightedDem / totalWeight,
            RepPercent = weightedRep / totalWeight,
            PollCount = totalPolls,
            LatestPollDate = latestPoll,
            Confidence = averages.Max(a => a.Confidence) // Use highest confidence
        };
    }
}
