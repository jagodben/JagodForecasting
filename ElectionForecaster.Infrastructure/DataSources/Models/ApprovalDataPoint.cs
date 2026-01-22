namespace ElectionForecaster.Infrastructure.DataSources.Models;

/// <summary>
/// Represents a single approval rating data point.
/// </summary>
public class ApprovalDataPoint
{
    /// <summary>
    /// The date of this data point.
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Approval percentage (0.0 to 100.0).
    /// </summary>
    public double ApprovePercent { get; set; }

    /// <summary>
    /// Disapproval percentage (0.0 to 100.0).
    /// </summary>
    public double DisapprovePercent { get; set; }

    /// <summary>
    /// Net approval (approve - disapprove).
    /// </summary>
    public double NetApproval => ApprovePercent - DisapprovePercent;

    /// <summary>
    /// The source of this data point.
    /// </summary>
    public string? Source { get; set; }
}
