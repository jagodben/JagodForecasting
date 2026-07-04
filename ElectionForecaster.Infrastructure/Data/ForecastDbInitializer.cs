using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;

namespace ElectionForecaster.Infrastructure.Data;

/// <summary>
/// Applies EF Core migrations at startup, bridging databases that predate migrations.
/// </summary>
public static class ForecastDbInitializer
{
    // Matches the EF Core version referenced by the project.
    private const string EfProductVersion = "8.0.11";

    public static void Initialize(ForecastDbContext context)
    {
        var db = context.Database;
        var creator = db.GetService<IRelationalDatabaseCreator>();
        var history = db.GetService<IHistoryRepository>();

        // If the database already exists but has no __EFMigrationsHistory table, it was created by the
        // old EnsureCreated() path. Its schema matches InitialCreate, so record that migration as
        // already applied; Migrate() will then run only the newer migrations (e.g. the margin columns)
        // instead of trying to re-create tables that already exist.
        if (creator.Exists() && !history.Exists())
        {
            db.ExecuteSqlRaw(history.GetCreateIfNotExistsScript());
            var initialMigration = db.GetService<IMigrationsAssembly>().Migrations.Keys.First();
            db.ExecuteSqlRaw(history.GetInsertScript(new HistoryRow(initialMigration, EfProductVersion)));
        }

        db.Migrate();
    }
}
