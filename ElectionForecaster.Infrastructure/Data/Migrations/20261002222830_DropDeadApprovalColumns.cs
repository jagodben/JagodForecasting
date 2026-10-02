using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElectionForecaster.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropDeadApprovalColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovalAdjustment",
                table: "ForecastHistory");

            migrationBuilder.DropColumn(
                name: "ApprovalWeight",
                table: "ForecastHistory");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "ApprovalAdjustment",
                table: "ForecastHistory",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ApprovalWeight",
                table: "ForecastHistory",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);
        }
    }
}
