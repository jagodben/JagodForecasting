using ElectionForecaster.Core.Enums;
using ElectionForecaster.Infrastructure.DataSources.Models;
using Microsoft.Extensions.Configuration;

namespace ElectionForecaster.Infrastructure.Forecasting;

/// <summary>
/// Calculates dynamic weights for blending the three forecast signals — polling,
/// fundamentals, and prediction markets — in margin space. Polling gains weight as the
/// election nears; fundamentals carry the early forecast; markets are a smaller sanity
/// input (they largely re-digest the same polls and carry a favorite-longshot bias).
/// The national environment / approval is folded into fundamentals, not weighted here.
/// </summary>
public class WeightCalculator
{
    private readonly double _defaultMarketWeight;
    private readonly double _defaultPollingWeight;
    private readonly double _defaultFundamentalsWeight;
    private readonly DateTime _electionDate;

    public WeightCalculator(IConfiguration configuration)
    {
        var section = configuration.GetSection("Forecasting:DefaultWeights");
        _defaultMarketWeight = section.GetValue<double>("PredictionMarkets", 0.15);
        _defaultPollingWeight = section.GetValue<double>("Polling", 0.45);
        _defaultFundamentalsWeight = section.GetValue<double>("Fundamentals", 0.40);

        var electionDateStr = configuration.GetValue<string>("Forecasting:ElectionDate") ?? "2026-11-03";
        _electionDate = DateTime.Parse(electionDateStr);
    }

    public ForecastWeights CalculateWeights(
        MarketOdds? marketOdds,
        PollingAverage? polling,
        FundamentalsData? fundamentals,
        RaceType raceType,
        DateTime? asOf = null)
    {
        var daysToElection = (_electionDate - (asOf ?? DateTime.UtcNow)).TotalDays;

        var weights = new ForecastWeights
        {
            MarketWeight = _defaultMarketWeight,
            PollingWeight = _defaultPollingWeight,
            FundamentalsWeight = _defaultFundamentalsWeight
        };

        AdjustForDataAvailability(weights, marketOdds, polling, fundamentals);
        AdjustForTimeToElection(weights, daysToElection);
        AdjustForRaceType(weights, raceType);
        weights.Normalize();
        return weights;
    }

    private void AdjustForDataAvailability(
        ForecastWeights weights,
        MarketOdds? marketOdds,
        PollingAverage? polling,
        FundamentalsData? fundamentals)
    {
        // No market → shift its weight mostly to fundamentals.
        if (marketOdds == null)
        {
            weights.FundamentalsWeight += weights.MarketWeight * 0.6;
            weights.PollingWeight += weights.MarketWeight * 0.4;
            weights.MarketWeight = 0;
        }
        else
        {
            weights.MarketWeight *= marketOdds.Confidence; // scale by market liquidity/volume
        }

        // No polling → lean on fundamentals (and a bit of market, only if a market exists).
        if (polling == null || polling.PollCount == 0)
        {
            if (marketOdds != null)
            {
                weights.FundamentalsWeight += weights.PollingWeight * 0.7;
                weights.MarketWeight += weights.PollingWeight * 0.3;
            }
            else
            {
                weights.FundamentalsWeight += weights.PollingWeight;
            }
            weights.PollingWeight = 0;
        }
        else
        {
            weights.PollingWeight *= polling.Confidence;
        }

        // No fundamentals (shouldn't happen) → redistribute to polling/market.
        if (fundamentals == null)
        {
            weights.PollingWeight += weights.FundamentalsWeight * 0.6;
            weights.MarketWeight += weights.FundamentalsWeight * 0.4;
            weights.FundamentalsWeight = 0;
        }
    }

    private void AdjustForTimeToElection(ForecastWeights weights, double daysToElection)
    {
        // Base weights represent the ~2-6 month window. Shift toward fundamentals when far
        // out (few/no polls, mood not yet set) and toward polling as election day nears.
        if (daysToElection > 180)
        {
            weights.FundamentalsWeight *= 1.6;
            weights.PollingWeight *= 0.45;
        }
        else if (daysToElection > 60)
        {
            // ~2-6 months: use base weights.
        }
        else if (daysToElection > 14)
        {
            weights.PollingWeight *= 1.35;
            weights.FundamentalsWeight *= 0.65;
            weights.MarketWeight *= 0.9;
        }
        else
        {
            weights.PollingWeight *= 1.5;
            weights.FundamentalsWeight *= 0.5;
            weights.MarketWeight *= 0.85;
        }
    }

    private void AdjustForRaceType(ForecastWeights weights, RaceType raceType)
    {
        switch (raceType)
        {
            case RaceType.Senate:
                weights.PollingWeight *= 1.1;   // well polled
                break;
            case RaceType.Governor:
                weights.PollingWeight *= 1.05;
                break;
            case RaceType.House:
                // Most House races are unpolled, so lean on fundamentals (generic ballot + PVI).
                // Don't extra-penalize polling: it's already zeroed when absent, so a ×0.7 here would
                // only ever hurt the rare genuinely-polled House race (e.g. Alaska), which we want to
                // trust like any other polled race.
                weights.FundamentalsWeight *= 1.4;
                weights.MarketWeight *= 0.6;
                break;
        }
    }
}

/// <summary>Weights for blending the three forecast signals (sum to 1 after Normalize).</summary>
public class ForecastWeights
{
    public double MarketWeight { get; set; }
    public double PollingWeight { get; set; }
    public double FundamentalsWeight { get; set; }

    public void Normalize()
    {
        var total = MarketWeight + PollingWeight + FundamentalsWeight;
        if (total > 0)
        {
            MarketWeight /= total;
            PollingWeight /= total;
            FundamentalsWeight /= total;
        }
        else
        {
            MarketWeight = PollingWeight = FundamentalsWeight = 1.0 / 3.0;
        }
    }
}
