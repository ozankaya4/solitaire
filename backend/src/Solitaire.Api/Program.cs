using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Solitaire.Api.Account;
using Solitaire.Api.Auth;
using Solitaire.Api.Data;
using Solitaire.Api.Leaderboard;
using Solitaire.Api.Security;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;
var env = builder.Environment;
var isTesting = env.IsEnvironment("Testing");

// Bind to the platform-provided port in production (Render/Railway/Fly set PORT).
// In dev this is unset and the launchSettings URL (5080) is used.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// -- Database: SQLite for dev/test, PostgreSQL (Npgsql) for prod -------------
// Provider is chosen by config (Database:Provider), defaulting by environment.
// Blank values (the appsettings.json placeholders, or a mis-typed env var) count
// as "unset" so an empty string doesn't defeat the environment default.
var configuredProvider = config["Database:Provider"];
var provider = string.IsNullOrWhiteSpace(configuredProvider)
    ? (env.IsProduction() ? "Postgres" : "Sqlite")
    : configuredProvider.Trim();
var usePostgres = string.Equals(provider, "Postgres", StringComparison.OrdinalIgnoreCase);

builder.Services.AddDbContext<AppDbContext>(options =>
{
    // Read the connection string HERE (lazily), not at top level: test hosts
    // (WebApplicationFactory) inject their connection string during Build, after
    // top-level statements run. A blank value counts as "unset".
    var connectionString = config.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        connectionString = null;
    }

    if (usePostgres)
    {
        // Fail loudly rather than silently falling back to an empty SQLite database
        // — that only surfaces later as confusing "no such table" 500s. The usual
        // cause is naming the env var with a single underscore
        // (ConnectionStrings_DefaultConnection) instead of the double underscore
        // .NET configuration requires.
        options.UseNpgsql(connectionString ?? throw new InvalidOperationException(
            "Database:Provider is 'Postgres' but no connection string was found. Set the "
            + "ConnectionStrings__DefaultConnection environment variable (note the DOUBLE "
            + "underscores — a single underscore is ignored by .NET configuration)."));
    }
    else
    {
        options.UseSqlite(connectionString ?? "Data Source=solitaire.db");
    }
});

// -- Localization: en (default) + tr, resolved per request (Accept-Language) --
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
string[] supportedCultures = ["en", "tr"];

// -- Identity (registration, login, password hashing, lockout — defaults) ----
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false; // email confirmation not wired yet
        // Password + lockout policies use Identity's secure defaults.
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddErrorDescriber<LocalizedIdentityErrorDescriber>()
    .AddDefaultTokenProviders();

// Cookie session: HttpOnly + Secure + SameSite=Strict; return 401/403 (no redirects).
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "solitaire.auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    // Over the plain-HTTP test server, Always would drop the cookie; require it everywhere else.
    options.Cookie.SecurePolicy = isTesting ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
    options.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
});

builder.Services.AddAuthorization();

// -- CORS locked to the known frontend origin (credentials allowed) ----------
const string CorsPolicy = "frontend";
var allowedOrigins = config.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173"];
builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy =>
    policy.WithOrigins(allowedOrigins).AllowCredentials().WithMethods("GET", "POST").AllowAnyHeader()));

// -- Rate limiting: auth (per IP) and score submissions (per account) --------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("submit", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 12, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

// Anti-forgery (double-submit cookie) for authenticated state-changing endpoints.
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "solitaire.csrf";
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = isTesting ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});

builder.Services.AddScoped<GuestImportService>();
builder.Services.AddScoped<GameVerificationService>();
// Curated level->seed ladder is immutable for the process lifetime.
builder.Services.AddSingleton<Solitaire.Api.Leaderboard.LevelRegistry>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// Apply schema: migrations on Postgres (prod), model on SQLite (dev/test).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (usePostgres)
    {
        db.Database.Migrate();
    }
    else
    {
        db.Database.EnsureCreated();
    }
}

// -- Pipeline (order matters) ------------------------------------------------
// Behind the platform's TLS-terminating proxy in production: trust its
// X-Forwarded-Proto/-For so the app sees HTTPS (sets/accepts Secure cookies) and
// the real client IP (rate limiting). Only the single platform proxy fronts us.
if (!env.IsDevelopment() && !isTesting)
{
    var forwardedOptions = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    };
    forwardedOptions.KnownIPNetworks.Clear();
    forwardedOptions.KnownProxies.Clear();
    app.UseForwardedHeaders(forwardedOptions);
}

if (!env.IsDevelopment() && !isTesting)
{
    app.UseHsts();
}
if (!isTesting)
{
    app.UseHttpsRedirection();
}

app.UseSecurityHeaders();
app.UseRequestLocalization(options =>
{
    options.SetDefaultCulture("en").AddSupportedCultures(supportedCultures).AddSupportedUICultures(supportedCultures);
});
app.UseCors(CorsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapAuthEndpoints();
app.MapLeaderboardEndpoints();
app.MapAccountEndpoints();

app.Run();

// Exposed so the integration-test WebApplicationFactory can boot the app.
public partial class Program;
