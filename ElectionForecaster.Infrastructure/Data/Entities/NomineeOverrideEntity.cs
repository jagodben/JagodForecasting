using System.ComponentModel.DataAnnotations;

namespace ElectionForecaster.Infrastructure.Data.Entities;

/// <summary>
/// A race's general-election nominees as last scraped from Wikipedia by the daily candidate
/// refresh. Overrides the compile-time nominee data in <c>ElectionDataProvider</c>, so candidate
/// changes (primaries concluding, dropouts, replacements) show up without a redeploy. A null name
/// means Wikipedia doesn't list a resolved nominee for that side — the static data/placeholder
/// stays in effect for it.
/// </summary>
public class NomineeOverrideEntity
{
    /// <summary>Race id, e.g. "MI-SEN-2026", "MI-GOV-2026", "MI-07-2026".</summary>
    [Key]
    [MaxLength(20)]
    public string RaceId { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? DemName { get; set; }
    public bool DemIsIncumbent { get; set; }

    [MaxLength(120)]
    public string? RepName { get; set; }
    public bool RepIsIncumbent { get; set; }

    /// <summary>When this row was last written by the refresh (UTC).</summary>
    public DateTime UpdatedAt { get; set; }
}
