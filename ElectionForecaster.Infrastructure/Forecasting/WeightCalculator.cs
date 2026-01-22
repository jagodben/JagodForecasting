using ElectionForecaster.Core.Enums;
using ElectionForecaster.Infrastructure.DataSources.Models;
using Microsoft.Extensions.Configuration;

namespace ElectionForecaster.Infrastructure.Forecasting;

/// <summary>
/// Calculates dynamic weights for combining forecast inputs.
/// </summary>
public class WeightCalculator
{
    private readonly double _defaultMarketWeight;
    private readonly double _defaultPollingWeight;
    private readonly double _defaultFundamentalsWeight;
    private readonly double _defaultApprovalWeight;
    private readonly DateTime _electionDate;

    public WeightCalculator(IConfiguration configuration)
    {
        var forecastingSection = configuration.GetSection("Forecasting:DefaultWeights");
        _defaultMarketWeight = forecastingSection.GetValue<double>("PredictionMarkets", 0.30);
        _defaultPollingWeight = forecastingSection.GetValue<double>("Polling", 0.35);
        _defaultFundamentalsWeight = forecastingSection.GetValue<double>("Fundamentals", 0.20);
        _defaultApprovalWeight = forecastingSection.GetValue<double>("Approval", 0.15);

        var electionDateStr = configuration.GetValue<string>("Forecasting:ElectionDate") ?? "2026-11-03";
        _electionDate = DateTime.Parse(electionDateStr);
    }

    /// <summary>
    /// Calculate dynamic weights based on data availability and time to election.
    /// </summary>
    public ForecastWeights CalculateWeights(
        MarketOdds? marketOdds,
        PollingAverage? polling,
        FundamentalsData? fundamentals,
        double? approval,
        RaceType raceType)
    {
        var daysToElection = (_electionDate - DateTime.UtcNow).TotalDays;

        // Start with default weights
        var weights = new ForecastWeights
        {
            MarketWeight = _defaultMarketWeight,
            PollingWeight = _defaultPollingWeight,
            FundamentalsWeight = _defaultFundamentalsWeight,
            ApprovalWeight = _defaultApprovalWeight
        };

        // Adjust for data availability
        AdjustForDataAvailability(weights, marketOdds, polling, fundamentals, approval);

        // Adjust for time to election
        AdjustForTimeToElection(weights, daysToElection);

        // Adjust for race type
        AdjustForRaceType(weights, raceType);

        // Normalize weights to sum to 1
        weights.Normalize();

        return weights;
    }

    private void AdjustForDataAvailability(
        ForecastWeights weights,
        MarketOdds? marketOdds,
        PollingAverage? polling,
        FundamentalsData? fundamentals,
        double? approval)
    {
        // If no market data, redistribute weight to polling
        if (marketOdds == null)
        {
            weights.PollingWeight += weights.MarketWeight * 0.7;
            weights.FundamentalsWeight += weights.MarketWeight * 0.3;
            weights.MarketWeight = 0;
        }
        else
        {
            // Scale market weight by confidence (based on volume)
            weights.MarketWeight *= marketOdds.Confidence;
        }

        // If no polling data, rely more heavily on fundamentals
        if (polling == null || polling.PollCount == 0)
        {
            weights.FundamentalsWeight += weights.PollingWeight * 0.6;
            weights.MarketWeight += weights.PollingWeight * 0.4;
            weights.PollingWeight = 0;
        }
        else
        {
            // Scale polling weight by confidence
            weights.PollingWeight *= polling.Confidence;
        }

        // If no fundamentals, redistribute
        if (fundamentals == null)
        {
            weights.PollingWeight += weights.FundamentalsWeight * 0.6;
            weights.MarketWeight += weights.FundamentalsWeight * 0.4;
            weights.FundamentalsWeight = 0;
        }

        // If no approval data, redistribute to fundamentals
        if (!approval.HasValue)
        {
            weights.FundamentalsWeight += weights.ApprovalWeight;
            weights.ApprovalWeight = 0;
        }
    }

    private void AdjustForTimeToElection(ForecastWeights weights, double daysToElection)
    {
        // As election approaches:
        // - Polling becomes more predictive
        // - Fundamentals become less important
        // - Markets reflect more polling aggregation

        if (daysToElection > 365)
        {
            // More than a year out - fundamentals matter most
            weights.FundamentalsWeight *= 1.3;
            weights.ApprovalWeight *= 1.2;
            weights.PollingWeight *= 0.7;
            weights.MarketWeight *= 0.9;
        }
        else if (daysToElection > 180)
        {
            // 6-12 months out - balanced
            // Use default weights
        }
        else if (daysToElection > 60)
        {
            // 2-6 months out - polling gains importance
            weights.PollingWeight *= 1.2;
            weights.FundamentalsWeight *= 0.8;
        }
        else if (daysToElection > 14)
        {
            // Final two months - polling is most predictive
            weights.PollingWeight *= 1.4;
            weights.MarketWeight *= 1.1;
            weights.FundamentalsWeight *= 0.5;
            weights.ApprovalWeight *= 0.7;
        }
        else
        {
            // Final two weeks - polls and markets dominate
            weights.PollingWeight *= 1.5;
            weights.MarketWeight *= 1.3;
            weights.FundamentalsWeight *= 0.3;
            weights.ApprovalWeight *= 0.5;
        }
    }

    private void AdjustForRaceType(ForecastWeights weights, RaceType raceType)
    {
        switch (raceType)
        {
            case RaceType.Senate:
                // Senate races tend to have more polling
                weights.PollingWeight *= 1.1;
                break;

            case RaceType.Governor:
                // Governor races also well-polled
                weights.PollingWeight *= 1.05;
                break;

            case RaceType.House:
                // House races often under-polled, rely more on fundamentals
                weights.FundamentalsWeight *= 1.3;
                weights.PollingWeight *= 0.8;
                // Markets also less liquid for House races
                weights.MarketWeight *= 0.7;
                break;
        }
    }
}

/// <summary>
/// Weights for combining forecast inputs.
/// </summary>
public class ForecastWeights
{
    public double MarketWeight { get; set; }
    public double PollingWeight { get; set; }
    public double FundamentalsWeight { get; set; }
    public double ApprovalWeight { get; set; }

    /// <summary>
    /// Normalizes weights to sum to 1.0.
    /// </summary>
    public void Normalize()
    {
        var total = MarketWeight + PollingWeight + FundamentalsWeight + ApprovalWeight;
        if (total > 0)
        {
            MarketWeight /= total;
            PollingWeight /= total;
            FundamentalsWeight /= total;
            ApprovalWeight /= total;
        }
        else
        {
            // Fallback to equal weights
            MarketWeight = 0.25;
            PollingWeight = 0.25;
            FundamentalsWeight = 0.25;
            ApprovalWeight = 0.25;
        }
    }
}
