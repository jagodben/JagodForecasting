using ElectionForecaster.Infrastructure.DataSources.Models;

namespace ElectionForecaster.Infrastructure.DataSources.Interfaces;

/// <summary>Provides the structural (non-poll) inputs for a race's forecast.</summary>
public interface IFundamentalsSource
{
    /// <summary>Gets the fundamentals (PVI, prior result, incumbency magnitude) for a race.</summary>
    Task<FundamentalsData> GetFundamentalsAsync(string raceId, CancellationToken cancellationToken = default);
}
