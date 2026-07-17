using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElectionForecaster.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropApprovalRatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovalRatings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApprovalRatings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ApprovePercent = table.Column<double>(type: "REAL", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DisapprovePercent = table.Column<double>(type: "REAL", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalRatings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRatings_Date",
                table: "ApprovalRatings",
                column: "Date");
        }
    }
}
