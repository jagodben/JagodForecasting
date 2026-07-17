using ElectionForecaster.Infrastructure.Forecasting;

namespace ElectionForecaster.Infrastructure.DataSources.Models;

/// <summary>Weighted polling average for a race. Percentages 0..100; margins positive = Dem lead.</summary>
public class PollingAverage
{
    public string RaceId { get; set; } = string.Empty;
    public double DemPercent { get; set; }
    public double RepPercent { get; set; }

    /// <summary>
    /// The raw margin. Includes undecideds, so it's the figure shown in the polls card, not the one
    /// blended into the forecast.
    /// </summary>
    public double Margin => DemPercent - RepPercent;

    /// <summary>
    /// The two-party Dem margin in points — the raw margin rescaled as if undecideds split
    /// proportionally (e.g. D45/R43 → +2.3, not +2). Puts the poll on the same final-result scale
    /// as the market and fundamentals margins it's blended with.
    /// </summary>
    public double TwoPartyMargin => (DemPercent + RepPercent) > 0
        ? (DemPercent - RepPercent) / (DemPercent + RepPercent) * 100.0
        : Margin;

    public int PollCount { get; set; }
    public DateTime? LatestPollDate { get; set; }
    public int? AverageSampleSize { get; set; }

    /// <summary>Confidence in this average based on poll quantity and recency (0..1).</summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Win probability implied by the polling margin at the given margin standard error. Pass the
    /// race's model SE so the "Polls" lens isn't shown as more confident than the blended forecast:
    /// a D+3 margin is ~69% at SE 6, not the ~80% the old hardcoded SE 3.5 produced. Defaults to a
    /// typical statewide SE.
    /// </summary>
    public double GetDemWinProbability(double standardError = 6.0)
        => ForecastMath.MarginToProbability(TwoPartyMargin, standardError);

    public double GetRepWinProbability(double standardError = 6.0) => 1.0 - GetDemWinProbability(standardError);
}
