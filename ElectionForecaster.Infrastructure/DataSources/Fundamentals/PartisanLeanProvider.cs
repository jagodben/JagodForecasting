using ElectionForecaster.Core.Enums;
using ElectionForecaster.Infrastructure.Data;
using ElectionForecaster.Infrastructure.DataSources.Interfaces;
using ElectionForecaster.Infrastructure.DataSources.Models;
using Microsoft.Extensions.Logging;

namespace ElectionForecaster.Infrastructure.DataSources.Fundamentals;

/// <summary>
/// Builds each race's fundamentals from partisan lean index (positive = Dem lean), the seat's prior
/// result, and an incumbency magnitude.
/// </summary>
public class PartisanLeanProvider : IFundamentalsSource
{
    private readonly ILogger<PartisanLeanProvider> _logger;

    public PartisanLeanProvider(ILogger<PartisanLeanProvider> logger)
    {
        _logger = logger;
    }

    private double GetPartisanLean(string stateId, int? districtNumber)
    {
        stateId = stateId.ToUpperInvariant();

        if (districtNumber.HasValue)
            return PartisanLean.GetDistrictLean(stateId, districtNumber.Value);

        if (PartisanLean.StateLean.TryGetValue(stateId, out var pvi))
            return pvi;

        _logger.LogWarning("No PVI data for state {StateId}", stateId);
        return 0.0;
    }

    // Flat incumbency advantage in points. Smaller for the House, where the personal
    // vote has declined the most.
    private static double GetIncumbencyAdvantage(RaceType raceType) => raceType switch
    {
        RaceType.House => 2.0,
        _ => 3.0
    };

    public Task<FundamentalsData> GetFundamentalsAsync(string raceId, CancellationToken cancellationToken = default)
    {
        // Race-ID formats: "PA-SEN-2026" / "GA-GOV-2026" (statewide), "CA-01-2026" (House,
        // middle segment = district number).
        var parts = raceId.Split('-');
        if (parts.Length < 3)
        {
            return Task.FromResult(new FundamentalsData { RaceId = raceId });
        }

        var stateId = parts[0];
        var kind = parts[1];
        int? districtNumber = null;

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

        // Prior result: a designated independent challenger's own showing, else the statewide
        // table, else the district's real 2024 result. Districts in the ten mid-decade-redrawn
        // states have no 2024 result on current lines and stay on PVI + incumbency.
        var priorMargin = IndependentChallengers.GetPriorMargin(raceId) ?? StatewidePriorResults.GetPriorMargin(raceId);
        if (priorMargin is null && raceType == RaceType.House && districtNumber.HasValue)
        {
            var result2024 = DistrictElectionData.GetResult2024(stateId, districtNumber.Value);
            if (result2024.HasValue)
                priorMargin = -result2024.Value.Margin; // stored R-positive; flip to Dem margin
        }

        return Task.FromResult(new FundamentalsData
        {
            RaceId = raceId,
            PartisanLean = GetPartisanLean(stateId, districtNumber),
            // NationalEnvironment and IncumbentIsDem are filled by the orchestrator (from the
            // generic ballot and the race's candidates); this provider stays purely structural.
            NationalEnvironment = 0,
            IncumbentIsDem = null,
            IncumbencyAdvantage = GetIncumbencyAdvantage(raceType),
            PriorMargin = priorMargin
        });
    }
}
