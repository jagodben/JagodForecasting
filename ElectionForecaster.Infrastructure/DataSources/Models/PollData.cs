namespace ElectionForecaster.Infrastructure.DataSources.Models;

public class PollData
{
    public string RaceId { get; set; } = string.Empty;
    public string Pollster { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public int? SampleSize { get; set; }

    // Percentages 0..100; margin positive = Dem lead.
    public double DemPercent { get; set; }
    public double RepPercent { get; set; }
    public double Margin => DemPercent - RepPercent;

    public string? PollsterRating { get; set; }
    public string? Methodology { get; set; }

    /// <summary>"LV" likely voters, "RV" registered voters, "A" adults.</summary>
    public string? Population { get; set; }

    /// <summary>
    /// Whether the poll was sponsored by a partisan source (parsed from the Wikipedia "(D)"/"(R)"/"(I)"
    /// pollster tag into <see cref="Methodology"/>). Such polls systematically favor their sponsor.
    /// </summary>
    public bool IsPartisan => Methodology?.StartsWith("Partisan", StringComparison.OrdinalIgnoreCase) ?? false;

    public string? SourceUrl { get; set; }

    public double GetWeight(DateTime asOfDate)
    {
        double weight = 1.0;

        // Half-life of 14 days
        int daysOld = (int)(asOfDate - Date).TotalDays;
        weight *= Math.Pow(0.5, daysOld / 14.0);

        if (SampleSize.HasValue)
        {
            weight *= Math.Min(1.5, Math.Sqrt(SampleSize.Value / 500.0));
        }

        weight *= GetPollsterRatingMultiplier();

        // Likely-voter polls are more predictive of the actual electorate
        if (Population == "LV")
            weight *= 1.2;
        else if (Population == "A")
            weight *= 0.7;

        // A campaign's internal poll shouldn't move the average as much as an independent one
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
            _ => 0.9 // unknown pollster
        };
    }
}
