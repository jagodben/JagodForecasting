using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElectionForecaster.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPollMatchupCandidates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DemCandidate",
                table: "Polls",
                type: "TEXT",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepCandidate",
                table: "Polls",
                type: "TEXT",
                maxLength: 120,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DemCandidate",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "RepCandidate",
                table: "Polls");
        }
    }
}
