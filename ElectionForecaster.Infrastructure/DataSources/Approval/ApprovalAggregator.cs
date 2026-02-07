using ElectionForecaster.Infrastructure.Data;
using ElectionForecaster.Infrastructure.Data.Entities;
using ElectionForecaster.Infrastructure.DataSources.Interfaces;
using ElectionForecaster.Infrastructure.DataSources.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ElectionForecaster.Infrastructure.DataSources.Approval;

/// <summary>
/// Aggregates presidential approval data from multiple sources.
/// </summary>
public class ApprovalAggregator : IApprovalSource
{
    private readonly HttpClient _httpClient;
    private readonly ForecastDbContext _dbContext;
    private readonly ILogger<ApprovalAggregator> _logger;
    private double? _cachedApproval;
    private DateTime _lastRefresh = DateTime.MinValue;

    // FiveThirtyEight approval tracking URL
    private const string FiveThirtyEightApprovalUrl =
        "https://raw.githubusercontent.com/fivethirtyeight/data/master/polls/approval_polls.csv";

    // Historical baseline - 50% is neutral for midterm impact
    private const double NeutralApproval = 50.0;

    // Each point below neutral = ~0.3 additional seat loss for president's party
    private const double ApprovalImpactFactor = 0.3;

    public ApprovalAggregator(
        HttpClient httpClient,
        ForecastDbContext dbContext,
        ILogger<ApprovalAggregator> logger)
    {
        _httpClient = httpClient;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<double> GetPresidentialApprovalAsync(CancellationToken cancellationToken = default)
    {
        // Check cache
        if (_cachedApproval.HasValue && (DateTime.UtcNow - _lastRefresh).TotalHours < 12)
        {
            return _cachedApproval.Value;
        }

        // Check database for recent data
        var recentApproval = await _dbContext.ApprovalRatings
            .OrderByDescending(a => a.Date)
            .FirstOrDefaultAsync(cancellationToken);

        if (recentApproval != null && (DateTime.UtcNow - recentApproval.Date).TotalDays < 7)
        {
            _cachedApproval = recentApproval.ApprovePercent;
            return recentApproval.ApprovePercent;
        }

        // Fetch fresh data
        await RefreshAsync(cancellationToken);

        return _cachedApproval ?? NeutralApproval;
    }

    public async Task<List<ApprovalDataPoint>> GetApprovalHistoryAsync(int days = 90, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-days);

        var history = await _dbContext.ApprovalRatings
            .Where(a => a.Date >= cutoff)
            .OrderBy(a => a.Date)
            .Select(a => new ApprovalDataPoint
            {
                Date = a.Date,
                ApprovePercent = a.ApprovePercent,
                DisapprovePercent = a.DisapprovePercent,
                Source = a.Source
            })
            .ToListAsync(cancellationToken);

        return history;
    }

    public async Task<double> GetApprovalAdjustmentAsync(CancellationToken cancellationToken = default)
    {
        var approval = await GetPresidentialApprovalAsync(cancellationToken);

        // Calculate the adjustment for the president's party
        // Below 50% hurts, above 50% helps
        var deviation = approval - NeutralApproval;

        // Convert to vote share adjustment
        // e.g., 45% approval = -5 points * 0.3 = -1.5 point adjustment for president's party
        return deviation * ApprovalImpactFactor;
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        // FiveThirtyEight approval data disabled for now - data source not properly configured
        _logger.LogDebug("Approval data refresh skipped (disabled)");
        _cachedApproval = NeutralApproval; // Default to neutral
        _lastRefresh = DateTime.UtcNow;
        await Task.CompletedTask;
    }

    private async Task<List<ApprovalDataPoint>> FetchFiveThirtyEightApprovalAsync(CancellationToken cancellationToken)
    {
        var results = new List<ApprovalDataPoint>();

        try
        {
            var csv = await _httpClient.GetStringAsync(FiveThirtyEightApprovalUrl, cancellationToken);
            var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length <= 1) return results;

            var headers = ParseCsvLine(lines[0]);
            var columns = BuildColumnMap(headers);

            // Get relevant column indices
            var dateIdx = GetColumnIndex(columns, "end_date", "created_at", "modeldate");
            var approveIdx = GetColumnIndex(columns, "approve", "yes", "approve_estimate");
            var disapproveIdx = GetColumnIndex(columns, "disapprove", "no", "disapprove_estimate");
            var pollsterIdx = GetColumnIndex(columns, "pollster", "pollster_name");
            var subjectIdx = GetColumnIndex(columns, "politician", "subject", "answer");

            if (dateIdx < 0 || approveIdx < 0) return results;

            // Only get recent data (last 90 days)
            var cutoff = DateTime.UtcNow.AddDays(-90);

            for (int i = 1; i < lines.Length; i++)
            {
                try
                {
                    var values = ParseCsvLine(lines[i]);

                    // Filter for current president if possible
                    if (subjectIdx >= 0 && values.Length > subjectIdx)
                    {
                        var subject = values[subjectIdx].ToLowerInvariant();
                        // Filter for presidential approval (not generic Biden/Trump but current president)
                        if (!subject.Contains("president") && !subject.Contains("trump") && !subject.Contains("biden"))
                        {
                            continue;
                        }
                    }

                    if (!DateTime.TryParse(values[dateIdx], out var date))
                        continue;

                    if (date < cutoff) continue;

                    if (!double.TryParse(values[approveIdx], out var approve))
                        continue;

                    double disapprove = 0;
                    if (disapproveIdx >= 0 && values.Length > disapproveIdx)
                    {
                        double.TryParse(values[disapproveIdx], out disapprove);
                    }

                    string? pollster = null;
                    if (pollsterIdx >= 0 && values.Length > pollsterIdx)
                    {
                        pollster = values[pollsterIdx].Trim();
                    }

                    results.Add(new ApprovalDataPoint
                    {
                        Date = date,
                        ApprovePercent = approve,
                        DisapprovePercent = disapprove,
                        Source = string.IsNullOrEmpty(pollster) ? "FiveThirtyEight" : pollster
                    });
                }
                catch
                {
                    // Skip malformed lines
                }
            }

            // Calculate aggregated daily averages
            results = results
                .GroupBy(a => a.Date.Date)
                .Select(g => new ApprovalDataPoint
                {
                    Date = g.Key,
                    ApprovePercent = g.Average(a => a.ApprovePercent),
                    DisapprovePercent = g.Average(a => a.DisapprovePercent),
                    Source = "FiveThirtyEight"
                })
                .OrderByDescending(a => a.Date)
                .Take(90) // Keep only 90 days
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching approval data from FiveThirtyEight");
        }

        return results;
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
                result.Add(current.Trim());
                current = "";
            }
            else
            {
                current += c;
            }
        }
        result.Add(current.Trim());

        return result.ToArray();
    }

    private static Dictionary<string, int> BuildColumnMap(string[] headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Length; i++)
        {
            map[headers[i].Trim()] = i;
        }
        return map;
    }

    private static int GetColumnIndex(Dictionary<string, int> columns, params string[] names)
    {
        foreach (var name in names)
        {
            if (columns.TryGetValue(name, out var idx))
                return idx;
        }
        return -1;
    }
}
