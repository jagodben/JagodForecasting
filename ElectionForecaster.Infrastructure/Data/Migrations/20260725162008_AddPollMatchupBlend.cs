using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElectionForecaster.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPollMatchupBlend : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "BlendDemPercent",
                table: "Polls",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "BlendRepPercent",
                table: "Polls",
                type: "REAL",
                nullable: true);

            // Existing rows predate blending and are all single-matchup rows.
            migrationBuilder.AddColumn<int>(
                name: "MatchupCount",
                table: "Polls",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<double>(
                name: "MatchupSpread",
                table: "Polls",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BlendDemPercent",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "BlendRepPercent",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "MatchupCount",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "MatchupSpread",
                table: "Polls");
        }
    }
}
