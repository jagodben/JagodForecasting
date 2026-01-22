using System.ComponentModel.DataAnnotations;

namespace ElectionForecaster.Infrastructure.Data.Entities;

/// <summary>
/// Stores generic congressional ballot polling averages.
/// </summary>
public class GenericBallotEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    public DateTime Date { get; set; }

    public double DemPercent { get; set; }
    public double RepPercent { get; set; }

    [MaxLength(100)]
    public string? Source { get; set; }

    // Net margin (Dem - Rep), positive = Dem lead
    public double Margin => DemPercent - RepPercent;
}
