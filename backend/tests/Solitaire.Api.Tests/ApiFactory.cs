using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Solitaire.Api.Tests;

/// <summary>
/// Boots the real API in the "Testing" environment against an isolated SQLite
/// database file (created via EnsureCreated at startup). HTTPS redirection and
/// Secure-cookie enforcement are relaxed in Testing so the plain-HTTP test server
/// can round-trip the auth cookie.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"sol-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Sqlite",
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={_dbPath}",
                ["Cors:AllowedOrigins:0"] = "http://localhost:5173",
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            try
            {
                File.Delete(_dbPath);
            }
            catch (IOException)
            {
                // best-effort cleanup
            }
        }
    }
}
