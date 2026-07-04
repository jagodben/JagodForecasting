using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElectionForecaster.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApprovalRatings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ApprovePercent = table.Column<double>(type: "REAL", nullable: false),
                    DisapprovePercent = table.Column<double>(type: "REAL", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalRatings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChamberHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Chamber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DemControlProbability = table.Column<double>(type: "REAL", nullable: false),
                    RepControlProbability = table.Column<double>(type: "REAL", nullable: false),
                    ExpectedDemSeats = table.Column<double>(type: "REAL", nullable: false),
                    ExpectedRepSeats = table.Column<double>(type: "REAL", nullable: false),
                    SimulationIterations = table.Column<int>(type: "INTEGER", nullable: false),
                    DemSeatsLow = table.Column<int>(type: "INTEGER", nullable: false),
                    DemSeatsHigh = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChamberHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ForecastHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RaceId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DemWinProbability = table.Column<double>(type: "REAL", nullable: false),
                    RepWinProbability = table.Column<double>(type: "REAL", nullable: false),
                    DemVoteShare = table.Column<double>(type: "REAL", nullable: false),
                    RepVoteShare = table.Column<double>(type: "REAL", nullable: false),
                    Confidence = table.Column<double>(type: "REAL", nullable: false),
                    MarketWeight = table.Column<double>(type: "REAL", nullable: false),
                    PollingWeight = table.Column<double>(type: "REAL", nullable: false),
                    FundamentalsWeight = table.Column<double>(type: "REAL", nullable: false),
                    ApprovalWeight = table.Column<double>(type: "REAL", nullable: false),
                    MarketOdds = table.Column<double>(type: "REAL", nullable: true),
                    PollingAverage = table.Column<double>(type: "REAL", nullable: true),
                    FundamentalsPrediction = table.Column<double>(type: "REAL", nullable: true),
                    ApprovalAdjustment = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForecastHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GenericBallot",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DemPercent = table.Column<double>(type: "REAL", nullable: false),
                    RepPercent = table.Column<double>(type: "REAL", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GenericBallot", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketOdds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RaceId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DemOdds = table.Column<double>(type: "REAL", nullable: false),
                    RepOdds = table.Column<double>(type: "REAL", nullable: false),
                    Volume = table.Column<double>(type: "REAL", nullable: true),
                    ExternalMarketId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketOdds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Polls",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RaceId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Pollster = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SampleSize = table.Column<int>(type: "INTEGER", nullable: true),
                    DemPercent = table.Column<double>(type: "REAL", nullable: false),
                    RepPercent = table.Column<double>(type: "REAL", nullable: false),
                    PollsterRating = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    Methodology = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Population = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    SourceUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Polls", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRatings_Date",
                table: "ApprovalRatings",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_ChamberHistory_Chamber",
                table: "ChamberHistory",
                column: "Chamber");

            migrationBuilder.CreateIndex(
                name: "IX_ChamberHistory_Chamber_Date",
                table: "ChamberHistory",
                columns: new[] { "Chamber", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ForecastHistory_Date",
                table: "ForecastHistory",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_ForecastHistory_RaceId",
                table: "ForecastHistory",
                column: "RaceId");

            migrationBuilder.CreateIndex(
                name: "IX_ForecastHistory_RaceId_Date",
                table: "ForecastHistory",
                columns: new[] { "RaceId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GenericBallot_Date",
                table: "GenericBallot",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_MarketOdds_RaceId",
                table: "MarketOdds",
                column: "RaceId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketOdds_RaceId_Source_Timestamp",
                table: "MarketOdds",
                columns: new[] { "RaceId", "Source", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketOdds_Timestamp",
                table: "MarketOdds",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Polls_Date",
                table: "Polls",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Polls_RaceId",
                table: "Polls",
                column: "RaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Polls_RaceId_Date",
                table: "Polls",
                columns: new[] { "RaceId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovalRatings");

            migrationBuilder.DropTable(
                name: "ChamberHistory");

            migrationBuilder.DropTable(
                name: "ForecastHistory");

            migrationBuilder.DropTable(
                name: "GenericBallot");

            migrationBuilder.DropTable(
                name: "MarketOdds");

            migrationBuilder.DropTable(
                name: "Polls");
        }
    }
}
