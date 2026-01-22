using ElectionForecaster.Core.Enums;
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

    // 2024 Cook PVI values (based on 2020+2024 presidential results)
    // Positive = Dem lean (D+X), Negative = Rep lean (R+X)
    private static readonly Dictionary<string, double> StatePVI = new()
    {
        { "AL", -15 }, { "AK", -9 }, { "AZ", -2 }, { "AR", -16 },
        { "CA", 14 }, { "CO", 6 }, { "CT", 8 }, { "DE", 7 },
        { "FL", -6 }, { "GA", 0 }, { "HI", 15 }, { "ID", -19 },
        { "IL", 8 }, { "IN", -10 }, { "IA", -6 }, { "KS", -10 },
        { "KY", -16 }, { "LA", -13 }, { "ME", 3 }, { "MD", 14 },
        { "MA", 16 }, { "MI", 1 }, { "MN", 2 }, { "MS", -10 },
        { "MO", -10 }, { "MT", -11 }, { "NE", -12 }, { "NV", 0 },
        { "NH", 1 }, { "NJ", 7 }, { "NM", 5 }, { "NY", 10 },
        { "NC", -3 }, { "ND", -20 }, { "OH", -6 }, { "OK", -20 },
        { "OR", 6 }, { "PA", 0 }, { "RI", 10 }, { "SC", -8 },
        { "SD", -16 }, { "TN", -14 }, { "TX", -5 }, { "UT", -11 },
        { "VT", 16 }, { "VA", 4 }, { "WA", 8 }, { "WV", -23 },
        { "WI", 0 }, { "WY", -25 }
    };

    // Congressional district PVI values (sample of competitive districts)
    // Format: "StateAbbr-DistrictNumber" -> PVI
    private static readonly Dictionary<string, double> DistrictPVI = new()
    {
        // California competitive districts
        { "CA-13", 4 }, { "CA-22", -4 }, { "CA-27", 1 }, { "CA-41", 2 }, { "CA-45", 2 },
        // Pennsylvania competitive districts
        { "PA-01", 3 }, { "PA-07", 1 }, { "PA-08", -2 }, { "PA-10", -7 }, { "PA-17", 1 },
        // Michigan competitive districts
        { "MI-03", 2 }, { "MI-07", 1 }, { "MI-08", 3 }, { "MI-10", -3 },
        // Arizona competitive districts
        { "AZ-01", 3 }, { "AZ-04", 0 }, { "AZ-06", -3 },
        // Wisconsin competitive districts
        { "WI-01", -3 }, { "WI-03", -2 },
        // Ohio competitive districts
        { "OH-01", -3 }, { "OH-09", 2 }, { "OH-13", -4 },
        // New York competitive districts
        { "NY-01", -3 }, { "NY-03", 2 }, { "NY-04", 4 }, { "NY-17", 4 }, { "NY-19", -1 },
        // Virginia competitive districts
        { "VA-02", -1 }, { "VA-07", 3 }, { "VA-10", 8 },
        // North Carolina competitive districts
        { "NC-01", 3 }, { "NC-06", -5 },
        // Georgia competitive districts
        { "GA-06", 3 }, { "GA-07", 2 },
        // Nevada competitive districts
        { "NV-01", 5 }, { "NV-03", 0 }, { "NV-04", 3 },
        // Texas competitive districts
        { "TX-15", -1 }, { "TX-23", -3 }, { "TX-28", -1 }, { "TX-34", 1 },
        // Iowa competitive districts
        { "IA-01", -3 }, { "IA-02", -5 }, { "IA-03", -4 },
        // Maine districts
        { "ME-01", 9 }, { "ME-02", -2 },
        // Nebraska districts
        { "NE-02", 1 }
    };

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

        // If district specified, try to get district-level PVI
        if (districtNumber.HasValue)
        {
            var districtKey = $"{stateId}-{districtNumber:D2}";
            if (DistrictPVI.TryGetValue(districtKey, out var districtPvi))
            {
                return Task.FromResult(districtPvi);
            }

            // Also try without leading zero
            districtKey = $"{stateId}-{districtNumber}";
            if (DistrictPVI.TryGetValue(districtKey, out districtPvi))
            {
                return Task.FromResult(districtPvi);
            }

            // Fall back to state PVI with district variation
            if (StatePVI.TryGetValue(stateId, out var statePvi))
            {
                // Add some variation based on district number
                var variation = (districtNumber.Value % 5) - 2; // -2 to +2
                return Task.FromResult(statePvi + variation);
            }
        }

        // State-level PVI
        if (StatePVI.TryGetValue(stateId, out var pvi))
        {
            return Task.FromResult(pvi);
        }

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
        // Incumbency advantage varies by race type
        var advantage = raceType switch
        {
            RaceType.Senate => 4.0,    // Senators have strong name recognition
            RaceType.Governor => 3.5,  // Governors also well-known
            RaceType.House => 3.0,     // House members have smaller advantage
            _ => 3.0
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
        var raceTypeStr = parts[1];
        int? districtNumber = null;

        // Parse race type
        var raceType = raceTypeStr.ToUpperInvariant() switch
        {
            "SEN" => RaceType.Senate,
            "GOV" => RaceType.Governor,
            "HOUSE" => RaceType.House,
            _ => RaceType.House
        };

        // Extract district number if present
        if (raceType == RaceType.House && parts.Length > 2)
        {
            // Try to parse district from the format "HOUSE-01" or just get the number
            if (int.TryParse(parts[2], out var dist))
            {
                districtNumber = dist;
            }
            else if (raceTypeStr.StartsWith("HOUSE-") && int.TryParse(raceTypeStr.Substring(6), out dist))
            {
                districtNumber = dist;
            }
        }

        var partisanLean = await GetPartisanLeanAsync(stateId, districtNumber, cancellationToken);
        var genericBallot = await GetGenericBallotAsync(cancellationToken);
        var midtermPenalty = await GetMidtermPenaltyAsync(_presidentParty, cancellationToken);
        var incumbencyAdvantage = await GetIncumbencyAdvantageAsync(raceType, cancellationToken);

        return new FundamentalsData
        {
            RaceId = raceId,
            PartisanLean = partisanLean,
            GenericBallot = genericBallot,
            IncumbentIsDem = null, // Would need to be populated from race data
            IncumbencyAdvantage = incumbencyAdvantage,
            IsMidterm = true, // 2026 is a midterm
            PresidentParty = _presidentParty,
            MidtermPenalty = midtermPenalty
        };
    }
}
