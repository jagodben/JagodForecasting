using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElectionForecaster.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNomineeOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NomineeOverrides",
                columns: table => new
                {
                    RaceId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    DemName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    DemIsIncumbent = table.Column<bool>(type: "INTEGER", nullable: false),
                    RepName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    RepIsIncumbent = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NomineeOverrides", x => x.RaceId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NomineeOverrides");
        }
    }
}
