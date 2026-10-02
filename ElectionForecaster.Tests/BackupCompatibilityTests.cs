using System.Text.Json;
using ElectionForecaster.Infrastructure.Data.Entities;

namespace ElectionForecaster.Tests;

/// <summary>
/// The nightly offsite backup is the restore path for the forecast history, and dumps taken before
/// a schema change keep the old fields. Dropping a column must not make those dumps unreadable.
/// </summary>
public class BackupCompatibilityTests
{
    // A real row from a backup taken before the dead approval columns were dropped. The import
    // endpoint deserializes with the same web defaults used here.
    private const string PreDropRow = """
        {"id": 42882, "raceId": "NE-SEN-2026", "date": "2026-09-19T00:00:00",
         "demWinProbability": 0.21690233952207688, "repWinProbability": 0.7830976604779232,
         "demVoteShare": 0.4818801679210542, "repVoteShare": 0.5181198320789457,
         "confidence": 0.8724641914029353, "expectedDemMargin": -3.623966415789161,
         "marginStdDev": 5.5, "marketWeight": 0.3625686691016444, "pollingWeight": 0,
         "fundamentalsWeight": 0.6374313308983555, "approvalWeight": 0,
         "marketOdds": 0.32499999999999996, "pollingAverage": null,
         "fundamentalsPrediction": 0.1687832771582391, "approvalAdjustment": null}
        """;

    [Fact]
    public void BackupRowWithDroppedApprovalColumnsStillRestores()
    {
        var row = JsonSerializer.Deserialize<ForecastHistoryEntity>(
            PreDropRow, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(row);
        Assert.Equal("NE-SEN-2026", row.RaceId);
        Assert.Equal(new DateTime(2026, 9, 19), row.Date);
        Assert.Equal(0.21690233952207688, row.DemWinProbability);
        Assert.Equal(-3.623966415789161, row.ExpectedDemMargin);
        Assert.Equal(0.6374313308983555, row.FundamentalsWeight);
        Assert.Equal(0.32499999999999996, row.MarketOdds);
        Assert.Null(row.PollingAverage);
    }
}
