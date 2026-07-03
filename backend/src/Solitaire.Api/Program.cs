using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Solitaire.Api.Auth;
using Solitaire.Api.Data;
using Solitaire.Api.Security;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;
var env = builder.Environment;
var isTesting = env.IsEnvironment("Testing");

// -- Database: SQLite for dev/test, PostgreSQL (Npgsql) for prod -------------
// Provider is chosen by config (Database:Provider), defaulting by environment.
var provider = config["Database:Provider"]
    ?? (env.IsProduction() ? "Postgres" : "Sqlite");
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = config.GetConnectionString("DefaultConnection");
    if (string.Equals(provider, "Postgres", StringComparison.OrdinalIgnoreCase))
    {
        options.UseNpgsql(connectionString
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required for Postgres."));
    }
    else
    {
        options.UseSqlite(connectionString ?? "Data Source=solitaire.db");
    }
});

// -- Identity (registration, login, password hashing, lockout — defaults) ----
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false; // email confirmation not wired yet
        // Password + lockout policies use Identity's secure defaults.
    })
    .AddEntityFrameworkStores<AppDbContext>()
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

// -- Rate limiting on auth endpoints -----------------------------------------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
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
builder.Services.AddProblemDetails();

var app = builder.Build();

// Apply schema: migrations on Postgres (prod), model on SQLite (dev/test).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (string.Equals(provider, "Postgres", StringComparison.OrdinalIgnoreCase))
    {
        db.Database.Migrate();
    }
    else
    {
        db.Database.EnsureCreated();
    }
}

// -- Pipeline (order matters) ------------------------------------------------
if (!env.IsDevelopment() && !isTesting)
{
    app.UseHsts();
}
if (!isTesting)
{
    app.UseHttpsRedirection();
}

app.UseSecurityHeaders();
app.UseCors(CorsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapAuthEndpoints();

app.Run();

// Exposed so the integration-test WebApplicationFactory can boot the app.
public partial class Program;
