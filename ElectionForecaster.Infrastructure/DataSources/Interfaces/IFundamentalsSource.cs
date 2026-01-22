using ElectionForecaster.Core.Enums;
using ElectionForecaster.Infrastructure.DataSources.Models;

namespace ElectionForecaster.Infrastructure.DataSources.Interfaces;

/// <summary>
/// Interface for fetching fundamental/structural election factors.
/// </summary>
public interface IFundamentalsSource
{
    /// <summary>
    /// Gets the Cook Partisan Voting Index (PVI) for a state or district.
    /// Returns a value like D+5 (positive = Dem lean) or R+3 (negative = Rep lean).
    /// </summary>
    Task<double> GetPartisanLeanAsync(string stateId, int? districtNumber = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current generic congressional ballot margin.
    /// Positive = Democratic lead, negative = Republican lead.
    /// </summary>
    Task<double> GetGenericBallotAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the expected midterm penalty for the president's party.
    /// Returns expected seat loss (positive number = seats lost).
    /// </summary>
    Task<double> GetMidtermPenaltyAsync(Party presidentParty, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the incumbency advantage in percentage points.
    /// </summary>
    Task<double> GetIncumbencyAdvantageAsync(RaceType raceType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all fundamentals data for a specific race.
    /// </summary>
    Task<FundamentalsData> GetFundamentalsAsync(string raceId, CancellationToken cancellationToken = default);
}
