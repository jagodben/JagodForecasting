using ElectionForecaster.Infrastructure.DataSources.Models;

namespace ElectionForecaster.Infrastructure.DataSources.Interfaces;

/// <summary>
/// Interface for fetching presidential approval data.
/// </summary>
public interface IApprovalSource
{
    /// <summary>
    /// Gets the current presidential approval rating average.
    /// </summary>
    Task<double> GetPresidentialApprovalAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets historical approval data for charting.
    /// </summary>
    Task<List<ApprovalDataPoint>> GetApprovalHistoryAsync(int days = 90, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates the approval-based adjustment for midterm forecasts.
    /// Returns a modifier to apply to the president's party forecasts.
    /// </summary>
    Task<double> GetApprovalAdjustmentAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes the cached approval data from the source.
    /// </summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);
}
