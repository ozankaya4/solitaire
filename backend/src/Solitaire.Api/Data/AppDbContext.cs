using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Solitaire.Api.Data;

/// <summary>
/// EF Core context: Identity tables plus per-account game saves and stats.
/// Primary keys are GUID/string (no database identity/sequence columns), which
/// keeps the schema portable across the SQLite (dev/test) and PostgreSQL (prod)
/// providers.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<GameSaveEntity> GameSaves => Set<GameSaveEntity>();

    public DbSet<PlayerStatEntity> PlayerStats => Set<PlayerStatEntity>();

    public DbSet<LeaderboardEntryEntity> LeaderboardEntries => Set<LeaderboardEntryEntity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<GameSaveEntity>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.Variant }).IsUnique();
            entity.Property(e => e.Variant).HasMaxLength(32);
            entity
                .HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PlayerStatEntity>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.Variant }).IsUnique();
            entity.Property(e => e.Variant).HasMaxLength(32);
            entity
                .HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<LeaderboardEntryEntity>(entity =>
        {
            // Dedupe: the same verified game cannot be recorded twice for a user.
            entity.HasIndex(e => new { e.UserId, e.GameHash }).IsUnique();
            // Leaderboard reads: per-variant, ranked by level (best per player).
            entity.HasIndex(e => new { e.Variant, e.Level });
            entity.Property(e => e.Variant).HasMaxLength(32);
            entity.Property(e => e.GameHash).HasMaxLength(64);
            entity
                .HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
