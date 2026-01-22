using ElectionForecaster.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ElectionForecaster.Infrastructure.Data;

public class ForecastDbContext : DbContext
{
    public ForecastDbContext(DbContextOptions<ForecastDbContext> options) : base(options)
    {
    }

    public DbSet<ForecastHistoryEntity> ForecastHistory { get; set; } = null!;
    public DbSet<PollEntity> Polls { get; set; } = null!;
    public DbSet<ChamberHistoryEntity> ChamberHistory { get; set; } = null!;
    public DbSet<MarketOddsEntity> MarketOdds { get; set; } = null!;
    public DbSet<ApprovalRatingEntity> ApprovalRatings { get; set; } = null!;
    public DbSet<GenericBallotEntity> GenericBallot { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ForecastHistory - unique constraint on RaceId + Date
        modelBuilder.Entity<ForecastHistoryEntity>(entity =>
        {
            entity.HasIndex(e => new { e.RaceId, e.Date }).IsUnique();
            entity.HasIndex(e => e.RaceId);
            entity.HasIndex(e => e.Date);
        });

        // Polls - index on RaceId and Date for efficient queries
        modelBuilder.Entity<PollEntity>(entity =>
        {
            entity.HasIndex(e => e.RaceId);
            entity.HasIndex(e => e.Date);
            entity.HasIndex(e => new { e.RaceId, e.Date });
        });

        // ChamberHistory - unique constraint on Chamber + Date
        modelBuilder.Entity<ChamberHistoryEntity>(entity =>
        {
            entity.HasIndex(e => new { e.Chamber, e.Date }).IsUnique();
            entity.HasIndex(e => e.Chamber);
        });

        // MarketOdds - index on RaceId and Timestamp
        modelBuilder.Entity<MarketOddsEntity>(entity =>
        {
            entity.HasIndex(e => e.RaceId);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => new { e.RaceId, e.Source, e.Timestamp });
        });

        // ApprovalRatings - index on Date
        modelBuilder.Entity<ApprovalRatingEntity>(entity =>
        {
            entity.HasIndex(e => e.Date);
        });

        // GenericBallot - index on Date
        modelBuilder.Entity<GenericBallotEntity>(entity =>
        {
            entity.HasIndex(e => e.Date);
        });
    }
}
