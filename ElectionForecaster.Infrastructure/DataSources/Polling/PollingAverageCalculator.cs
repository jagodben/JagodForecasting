using ElectionForecaster.Infrastructure.DataSources.Models;

namespace ElectionForecaster.Infrastructure.DataSources.Polling;

/// <summary>
/// Computes a weighted polling average from a set of individual polls.
/// Weighting (recency decay, sample size, pollster quality, likely-voter screen)
/// lives on <see cref="PollData.GetWeight"/>; this class turns a bag of polls into
/// a single <see cref="PollingAverage"/> plus a confidence score.
/// </summary>
public static class PollingAverageCalculator
{
    /// <summary>
    /// Weighted polling average. <paramref name="asOf"/> anchors the recency decay — pass a past
    /// date to reconstruct what the average looked like then (for the retrospective backfill).
    /// </summary>
    public static PollingAverage Calculate(IReadOnlyList<PollData> polls, string raceId, DateTime? asOf = null)
    {
        if (polls.Count == 0)
        {
            return new PollingAverage { RaceId = raceId };
        }

        var now = asOf ?? DateTime.UtcNow;
        double totalWeight = 0, weightedDem = 0, weightedRep = 0;
        int totalSampleSize = 0, sampleCount = 0;

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

        return new PollingAverage
        {
            RaceId = raceId,
            DemPercent = weightedDem / totalWeight,
            RepPercent = weightedRep / totalWeight,
            PollCount = polls.Count,
            LatestPollDate = polls.Max(p => p.Date),
            AverageSampleSize = sampleCount > 0 ? totalSampleSize / sampleCount : null,
            Confidence = CalculateConfidence(polls, now)
        };
    }

    /// <summary>
    /// Confidence in the average based on poll quantity, recency, and quality.
    /// Clamped to [0.3, 1.0].
    /// </summary>
    private static double CalculateConfidence(IReadOnlyList<PollData> polls, DateTime asOf)
    {
        double confidence = 0.5;

        // Poll count bonus (up to +0.2)
        confidence += Math.Min(0.2, polls.Count * 0.02);

        // Recency bonus (up to +0.15)
        var latestPoll = polls.Max(p => p.Date);
        var daysSinceLatest = (asOf - latestPoll).TotalDays;
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

    private static double GetRatingScore(string? rating) => rating switch
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
        _ => 0.5
    };
}
