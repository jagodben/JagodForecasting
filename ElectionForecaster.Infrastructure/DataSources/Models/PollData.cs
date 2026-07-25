namespace ElectionForecaster.Infrastructure.DataSources.Models;

public class PollData
{
    public string RaceId { get; set; } = string.Empty;
    public string Pollster { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public int? SampleSize { get; set; }

    // Percentages 0..100; margin positive = Dem lead. When a poll tested several hypothetical
    // matchups (undecided primary), these hold the first-listed matchup — the numbers shown on
    // the polls page — and the blend fields below hold what the model actually averages.
    public double DemPercent { get; set; }
    public double RepPercent { get; set; }
    public double Margin => DemPercent - RepPercent;

    /// <summary>
    /// Mean Dem/Rep percentages across every matchup this pollster tested on this date, when
    /// there was more than one (null otherwise). Neutral to who wins the primary: each tested
    /// pairing counts equally.
    /// </summary>
    public double? BlendDemPercent { get; set; }
    public double? BlendRepPercent { get; set; }

    /// <summary>Number of hypothetical matchups this poll tested (1 = nominees settled).</summary>
    public int MatchupCount { get; set; } = 1;

    /// <summary>
    /// Max − min margin (points) across the tested matchups — how much the race depends on who
    /// gets nominated. Zero for single-matchup polls; priced as extra SE by the orchestrator.
    /// </summary>
    public double MatchupSpread { get; set; }

    /// <summary>The percentages the model averages: the matchup blend when present, else the poll as shown.</summary>
    public double ModelDemPercent => BlendDemPercent ?? DemPercent;
    public double ModelRepPercent => BlendRepPercent ?? RepPercent;

    /// <summary>
    /// Candidate names from the matchup table's column headers, set at parse time so the blender
    /// can match matchups against settled nominees. Not persisted — every parse re-derives them.
    /// </summary>
    public string? DemCandidate { get; set; }
    public string? RepCandidate { get; set; }

    public string? PollsterRating { get; set; }
    public string? Methodology { get; set; }

    /// <summary>"LV" likely voters, "RV" registered voters, "A" adults.</summary>
    public string? Population { get; set; }

    /// <summary>
    /// Whether the poll was sponsored by a partisan source (parsed from the Wikipedia "(D)"/"(R)"/"(I)"
    /// pollster tag into <see cref="Methodology"/>). Such polls systematically favor their sponsor.
    /// </summary>
    public bool IsPartisan => Methodology?.StartsWith("Partisan", StringComparison.OrdinalIgnoreCase) ?? false;

    /// <summary>"D"/"R"/"I" from the stored "Partisan (X)" tag; null for public polls.</summary>
    public string? PartisanLean => PartisanLeanOf(Methodology);

    public static string? PartisanLeanOf(string? methodology)
    {
        var m = methodology == null
            ? null
            : System.Text.RegularExpressions.Regex.Match(methodology, @"^Partisan \(([DRI])\)");
        return m is { Success: true } ? m.Groups[1].Value : null;
    }

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
