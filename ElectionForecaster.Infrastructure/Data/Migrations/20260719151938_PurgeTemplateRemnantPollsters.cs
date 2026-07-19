using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElectionForecaster.Infrastructure.Data.Migrations
{
    /// <summary>
    /// One-shot data repair: two polls carried an unclosed {{cite ...}} template remnant in the
    /// pollster name (a multi-line template split across parsed cells). The parser now strips
    /// these; purge the bad rows so the next fetch re-persists them cleanly.
    /// </summary>
    public partial class PurgeTemplateRemnantPollsters : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM Polls WHERE Pollster LIKE '%{{%';");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
