using ElectionForecaster.Core.Enums;
using ElectionForecaster.Core.Interfaces;
using ElectionForecaster.Infrastructure.Data;
using ElectionForecaster.Infrastructure.DataSources.Fundamentals;
using ElectionForecaster.Infrastructure.DataSources.Interfaces;
using ElectionForecaster.Infrastructure.DataSources.Models;
using ElectionForecaster.Infrastructure.Forecasting;
using ElectionForecaster.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ElectionForecaster.Tests;

/// <summary>
/// Pins that the model has exactly one fundamentals model. RaceService once ran a second one (a
/// fixed midterm bonus, its own incumbency term, no prior results or independent challengers),
/// and every fallback path — the chamber simulation, the races and states endpoints — quietly
/// served its numbers instead of the forecast's. The fallback is now the orchestrator's own
/// fundamentals-only forecast, so these tests run the real orchestrator against real race data.
/// </summary>
public class BaselineForecastTests
{
    [Fact]
    public async Task BaselineIsTheFullModelWithFundamentalsAlone()
    {
        // With no market and no polls, a full forecast is fundamentals alone — so the baseline must
        // reproduce it exactly, for every race (independent challengers and ranked-choice races
        // included). Any second formula, anywhere, would show up as a mismatch here.
        using var harness = new Harness(new NoPolls());
        var orchestrator = harness.Orchestrator;

        var mismatches = new List<string>();
        foreach (var race in await harness.Races.GetAllRacesAsync())
        {
            var full = await orchestrator.GenerateForecastAsync(race.Id);
            var baseline = await orchestrator.GenerateBaselineForecastAsync(race.Id);

            if (Math.Abs(full.ExpectedDemMargin - baseline.ExpectedDemMargin) > 1e-9 ||
                Math.Abs(full.MarginStdDev - baseline.MarginStdDev) > 1e-9 ||
                Math.Abs(full.DemWinProbability - baseline.DemWinProbability) > 1e-9)
            {
                mismatches.Add($"{race.Id}: full {full.ExpectedDemMargin:F3} vs baseline {baseline.ExpectedDemMargin:F3}");
            }
        }

        Assert.Empty(mismatches);
    }

    [Fact]
    public async Task ChamberCountsARaceWhoseForecastFailed()
    {
        // A failing poll fetch drops Nebraska from GenerateAllForecastsAsync. The Senate simulation
        // must still count all 100 seats, filling the gap from the model's own baseline. (The old
        // fallback looked the challenger up as a Democrat, found Osborn in the slot instead, and
        // counted the seat as a coin flip.)
        using var harness = new Harness(new NoPolls(failFor: "NE-SEN-2026"));
        var orchestrator = harness.Orchestrator;

        var forecasts = await orchestrator.GenerateAllForecastsAsync(RaceType.Senate);
        Assert.DoesNotContain(forecasts, f => f.RaceId == "NE-SEN-2026");

        var senate = await orchestrator.SimulateChamberAsync(RaceType.Senate);
        Assert.Equal(100, senate.ExpectedDemSeats + senate.ExpectedRepSeats, 6);
    }

    /// <summary>No polls for any race; optionally fails outright for one, like a broken fetch.</summary>
    private sealed class NoPolls(string? failFor = null) : IPollingSource
    {
        public string SourceName => "None";

        public Task<PollingAverage?> GetPollingAverageAsync(string raceId, CancellationToken cancellationToken = default) =>
            raceId == failFor
                ? throw new HttpRequestException($"poll fetch failed for {raceId}")
                : Task.FromResult<PollingAverage?>(null);

        public Task<List<PollData>> GetRecentPollsAsync(string raceId, int days = 30, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<PollData>());

        public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FixedBallot(double? margin) : IGenericBallotSource
    {
        public Task<double?> GetCurrentMarginAsync(CancellationToken cancellationToken = default) => Task.FromResult(margin);
        public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>
    /// The production orchestrator wired the way Program.cs wires it, minus the network: no
    /// prediction markets, the given polling source, a fixed D+4 generic ballot, and a throwaway
    /// SQLite file (the parallel forecast path gives each race its own DbContext, which an
    /// in-memory connection can't be shared across).
    /// </summary>
    private sealed class Harness : IDisposable
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"forecast-test-{Guid.NewGuid():N}.db");
        private readonly ServiceProvider _services;
        private readonly IServiceScope _scope;

        public Harness(IPollingSource polling)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddMemoryCache();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            services.AddDbContext<ForecastDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
            services.AddSingleton<IRaceService, RaceService>();
            services.AddSingleton(polling);
            services.AddScoped<IFundamentalsSource, PartisanLeanProvider>();
            services.AddSingleton<IGenericBallotSource>(new FixedBallot(4.0));
            services.AddSingleton<WeightCalculator>();
            services.AddSingleton<MonteCarloSimulator>();
            services.AddScoped<IForecastingOrchestrator, ForecastingOrchestrator>();

            _services = services.BuildServiceProvider();
            _scope = _services.CreateScope();
            _scope.ServiceProvider.GetRequiredService<ForecastDbContext>().Database.EnsureCreated();
        }

        public IForecastingOrchestrator Orchestrator => _scope.ServiceProvider.GetRequiredService<IForecastingOrchestrator>();

        public IRaceService Races => _services.GetRequiredService<IRaceService>();

        public void Dispose()
        {
            _scope.Dispose();
            _services.Dispose();
            SqliteConnection.ClearAllPools();
            try { File.Delete(_dbPath); } catch (IOException) { }
        }
    }
}
