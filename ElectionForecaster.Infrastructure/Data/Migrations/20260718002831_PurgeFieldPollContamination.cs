using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElectionForecaster.Infrastructure.Data.Migrations
{
    /// <summary>
    /// One-shot data repair: multi-candidate field polls that slipped past the old sum&lt;60
    /// filter (AK-GOV D41/R19, RI-GOV D38/R22, SD-SEN D18/R43) poisoned these races' stored
    /// polls and their frozen forecast history. Purge both; fresh parses re-persist clean polls
    /// under the tightened PollFilters rule, and the startup backfill reseeds the history for
    /// any race left with no rows.
    /// </summary>
    public partial class PurgeFieldPollContamination : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DELETE FROM Polls WHERE RaceId IN ('AK-GOV-2026', 'RI-GOV-2026', 'SD-SEN-2026');");
            migrationBuilder.Sql(
                "DELETE FROM ForecastHistory WHERE RaceId IN ('AK-GOV-2026', 'RI-GOV-2026', 'SD-SEN-2026');");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
