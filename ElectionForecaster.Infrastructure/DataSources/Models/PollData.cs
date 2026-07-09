namespace ElectionForecaster.Infrastructure.DataSources.Models;

/// <summary>
/// Represents an individual poll result.
/// </summary>
public class PollData
{
    /// <summary>
    /// The race identifier.
    /// </summary>
    public string RaceId { get; set; } = string.Empty;

    /// <summary>
    /// The pollster name.
    /// </summary>
    public string Pollster { get; set; } = string.Empty;

    /// <summary>
    /// Date the poll was conducted/released.
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Sample size of the poll.
    /// </summary>
    public int? SampleSize { get; set; }

    /// <summary>
    /// Democratic candidate percentage (0.0 to 100.0).
    /// </summary>
    public double DemPercent { get; set; }

    /// <summary>
    /// Republican candidate percentage (0.0 to 100.0).
    /// </summary>
    public double RepPercent { get; set; }

    /// <summary>
    /// The margin (positive = Dem lead, negative = Rep lead).
    /// </summary>
    public double Margin => DemPercent - RepPercent;

    /// <summary>
    /// FiveThirtyEight pollster rating (e.g., "A+", "B-").
    /// </summary>
    public string? PollsterRating { get; set; }

    /// <summary>
    /// Poll methodology (e.g., "Live Phone", "Online Panel").
    /// </summary>
    public string? Methodology { get; set; }

    /// <summary>
    /// Population type (LV = Likely Voters, RV = Registered Voters, A = Adults).
    /// </summary>
    public string? Population { get; set; }

    /// <summary>
    /// Whether the poll was sponsored by a partisan source (parsed from the Wikipedia "(D)"/"(R)"/"(I)"
    /// pollster tag into <see cref="Methodology"/>). Such polls systematically favor their sponsor.
    /// </summary>
    public bool IsPartisan => Methodology?.StartsWith("Partisan", StringComparison.OrdinalIgnoreCase) ?? false;

    /// <summary>
    /// URL source of the poll data.
    /// </summary>
    public string? SourceUrl { get; set; }

    /// <summary>
    /// Gets the weight for this poll based on quality factors.
    /// </summary>
    public double GetWeight(DateTime asOfDate)
    {
        double weight = 1.0;

        // Time decay - polls lose half their weight every 14 days
        int daysOld = (int)(asOfDate - Date).TotalDays;
        weight *= Math.Pow(0.5, daysOld / 14.0);

        // Sample size bonus
        if (SampleSize.HasValue)
        {
            weight *= Math.Min(1.5, Math.Sqrt(SampleSize.Value / 500.0));
        }

        // Pollster rating bonus
        weight *= GetPollsterRatingMultiplier();

        // Likely voter polls are more predictive
        if (Population == "LV")
            weight *= 1.2;
        else if (Population == "A")
            weight *= 0.7;

        // Partisan-sponsored polls lean toward their sponsor, so count them at half weight — a
        // campaign's internal poll shouldn't move the average as much as an independent one.
        if (IsPartisan)
            weight *= 0.5;

        return weight;
    }

    private double GetPollsterRatingMultiplier()
    {
        return PollsterRating switch
        {
            "A+" => 1.4,
            "A" => 1.3,
            "A-" => 1.2,
            "A/B" => 1.15,
            "B+" => 1.1,
            "B" => 1.0,
            "B-" => 0.95,
            "B/C" => 0.9,
            "C+" => 0.85,
            "C" => 0.8,
            "C-" => 0.75,
            "C/D" => 0.7,
            "D+" => 0.65,
            "D" => 0.6,
            "D-" => 0.55,
            _ => 0.9 // Unknown pollster
        };
    }
}
