namespace ElectionForecaster.Infrastructure.DataSources.Interfaces;

/// <summary>
/// Provides the current generic congressional ballot average — the national two-party
/// vote-intention margin used as the forecast's national-environment term when available.
/// </summary>
public interface IGenericBallotSource
{
    /// <summary>
    /// Current generic-ballot Democratic margin in points (e.g. +5.8 = D+5.8), or null when
    /// no data is available (the caller then falls back to the baseline midterm environment).
    /// </summary>
    Task<double?> GetCurrentMarginAsync(CancellationToken cancellationToken = default);

    /// <summary>Refreshes the average from the upstream source.</summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);
}
