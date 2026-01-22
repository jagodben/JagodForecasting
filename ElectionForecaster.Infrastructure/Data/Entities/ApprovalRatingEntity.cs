using System.ComponentModel.DataAnnotations;

namespace ElectionForecaster.Infrastructure.Data.Entities;

/// <summary>
/// Stores presidential approval rating data over time.
/// </summary>
public class ApprovalRatingEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    public DateTime Date { get; set; }

    public double ApprovePercent { get; set; }
    public double DisapprovePercent { get; set; }

    [MaxLength(100)]
    public string? Source { get; set; } // "FiveThirtyEight", "Gallup", etc.

    // Net approval (approve - disapprove)
    public double NetApproval => ApprovePercent - DisapprovePercent;
}
