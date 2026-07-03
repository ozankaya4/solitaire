using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Solitaire.Api.Data;

/// <summary>
/// Design-time factory used by <c>dotnet ef</c>. It always targets the PRODUCTION
/// provider (PostgreSQL) so that generated migrations are Postgres-compatible,
/// regardless of the provider selected at runtime. No connection is opened during
/// scaffolding; the connection string is a placeholder.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=solitaire;Username=postgres;Password=postgres")
            .Options;
        return new AppDbContext(options);
    }
}
