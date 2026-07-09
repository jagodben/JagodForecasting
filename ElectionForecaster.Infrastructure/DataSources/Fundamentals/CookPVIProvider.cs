using ElectionForecaster.Core.Enums;
using ElectionForecaster.Infrastructure.Data;
using ElectionForecaster.Infrastructure.DataSources.Interfaces;
using ElectionForecaster.Infrastructure.DataSources.Models;
using Microsoft.Extensions.Logging;

namespace ElectionForecaster.Infrastructure.DataSources.Fundamentals;

/// <summary>
/// Provides Cook Partisan Voting Index (PVI) data for states and districts.
/// PVI measures how a state/district votes compared to the nation as a whole.
/// Positive values = Democratic lean, Negative values = Republican lean.
/// </summary>
public class CookPVIProvider : IFundamentalsSource
{
    private readonly ILogger<CookPVIProvider> _logger;

    // Generic ballot tracker (current average)
    private double _genericBallot = 0.0; // Will be updated from polling

    // Presidential approval (affects midterm penalty)
    private readonly Party _presidentParty = Party.Republican; // 2025-2029 presidency
    private const double BaseMidtermPenalty = 4.0; // Typical points lost by president's party

    public CookPVIProvider(ILogger<CookPVIProvider> logger)
    {
        _logger = logger;
    }

    public Task<double> GetPartisanLeanAsync(string stateId, int? districtNumber = null, CancellationToken cancellationToken = default)
    {
        stateId = stateId.ToUpperInvariant();

        // Cook PVI as a Dem lean (district table falls back to state; see CookPvi).
        if (districtNumber.HasValue)
            return Task.FromResult(CookPvi.GetDistrictLean(stateId, districtNumber.Value));

        if (CookPvi.StateLean.TryGetValue(stateId, out var pvi))
            return Task.FromResult(pvi);

        _logger.LogWarning("No PVI data for state {StateId}", stateId);
        return Task.FromResult(0.0);
    }

    public Task<double> GetGenericBallotAsync(CancellationToken cancellationToken = default)
    {
        // This would be updated from polling sources
        return Task.FromResult(_genericBallot);
    }

    public void UpdateGenericBallot(double margin)
    {
        _genericBallot = margin;
    }

    public Task<double> GetMidtermPenaltyAsync(Party presidentParty, CancellationToken cancellationToken = default)
    {
        // Midterm penalty varies by:
        // 1. Base historical average (~4 points)
        // 2. Presidential approval
        // 3. Economic conditions

        // 2026 is a midterm with a Republican president (assumed)
        // The penalty applies to the president's party (Republicans)
        // So a positive penalty means Democrats gain an advantage
        if (presidentParty == _presidentParty)
        {
            return Task.FromResult(BaseMidtermPenalty);
        }

        return Task.FromResult(0.0);
    }

    public Task<double> GetIncumbencyAdvantageAsync(RaceType raceType, CancellationToken cancellationToken = default)
    {
        // Incumbency advantage varies by race type. Magnitudes reflect the sharp post-2008
        // decline in the personal incumbency vote (nationalization / straight-ticket voting).
        var advantage = raceType switch
        {
            RaceType.Senate => 3.0,    // strong name rec, but faces well-funded challengers
            RaceType.Governor => 3.0,  // retains some personal vote
            RaceType.House => 2.0,     // declined the most — district ≈ partisanship now
            _ => 2.0
        };

        return Task.FromResult(advantage);
    }

    public async Task<FundamentalsData> GetFundamentalsAsync(string raceId, CancellationToken cancellationToken = default)
    {
        // Parse race ID (format: "PA-SEN-2026" or "PA-HOUSE-01-2026")
        var parts = raceId.Split('-');
        if (parts.Length < 3)
        {
            return new FundamentalsData { RaceId = raceId };
        }

        var stateId = parts[0];
        var kind = parts[1];
        int? districtNumber = null;

        // Race-ID formats: statewide is "PA-SEN-2026" / "GA-GOV-2026"; House is "CA-01-2026",
        // where the middle segment is the district number (NOT a literal "HOUSE").
        RaceType raceType;
        if (kind.Equals("SEN", StringComparison.OrdinalIgnoreCase))
            raceType = RaceType.Senate;
        else if (kind.Equals("GOV", StringComparison.OrdinalIgnoreCase))
            raceType = RaceType.Governor;
        else if (int.TryParse(kind, out var dist))
        {
            raceType = RaceType.House;
            districtNumber = dist;
        }
        else
            raceType = RaceType.House;

        var partisanLean = await GetPartisanLeanAsync(stateId, districtNumber, cancellationToken);
        var incumbencyAdvantage = await GetIncumbencyAdvantageAsync(raceType, cancellationToken);

        return new FundamentalsData
        {
            RaceId = raceId,
            PartisanLean = partisanLean,
            // NationalEnvironment and IncumbentIsDem are cross-cutting: the orchestrator fills them
            // from the approval source and the race's candidates respectively. Left at defaults here
            // so this provider stays purely structural (PVI + incumbency magnitude).
            NationalEnvironment = 0,
            IncumbentIsDem = null,
            IncumbencyAdvantage = incumbencyAdvantage
        };
    }
}
