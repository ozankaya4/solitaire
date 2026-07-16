using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Solitaire.Api.Auth;
using Solitaire.Api.Data;
using Solitaire.Engine;

namespace Solitaire.Api.Leaderboard;

public static class LeaderboardEndpoints
{
    private const int MaxTop = 100;

    public static void MapLeaderboardEndpoints(this IEndpointRouteBuilder routes)
    {
        // Submissions: authenticated only (guests never submit) + per-account rate limit.
        routes
            .MapPost("/api/games/submit", SubmitAsync)
            .RequireAuthorization()
            .RequireRateLimiting("submit");

        // Reads are public; the player's own rank is included when signed in.
        routes.MapGet("/api/leaderboard/{variant}", GetLeaderboardAsync).AllowAnonymous();
    }

    private static async Task<IResult> SubmitAsync(
        SubmitGameRequest request,
        GameVerificationService verification,
        UserManager<ApplicationUser> userManager,
        AppDbContext db,
        IStringLocalizer<ApiMessages> localizer,
        ClaimsPrincipal principal,
        CancellationToken ct)
    {
        if (!AuthEndpoints.TryValidate(request, localizer, out var errors))
        {
            return Results.ValidationProblem(errors);
        }

        string? userId = userManager.GetUserId(principal);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var result = await verification.VerifyAndRecordAsync(userId, request, ct);
        return result.Verdict switch
        {
            GameVerificationService.Verdict.Accepted => Results.Ok(new SubmitGameResponse(
                result.Entry!.Level,
                result.Entry.Score,
                result.Entry.TimeMs,
                await RankOfAsync(db, result.Entry.Variant, userId, ct) ?? 0)),
            GameVerificationService.Verdict.Duplicate => Results.Conflict(
                new { error = localizer["Submit.Duplicate"].Value }),
            // The submission was understood but failed verification — say only that.
            _ => Results.UnprocessableEntity(new { error = localizer["Submit.Failed"].Value }),
        };
    }

    private static async Task<IResult> GetLeaderboardAsync(
        string variant,
        int? top,
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IStringLocalizer<ApiMessages> localizer,
        ClaimsPrincipal principal,
        CancellationToken ct)
    {
        if (!SolitaireEngines.TryGet(variant, out var engine))
        {
            return Results.NotFound(new { error = localizer["Leaderboard.UnknownVariant", variant].Value });
        }

        int take = Math.Clamp(top ?? 10, 1, MaxTop);

        // Highest level reached per player.
        var bests = await db.LeaderboardEntries
            .Where(e => e.Variant == engine.Variant)
            .GroupBy(e => e.UserId)
            .Select(g => new { UserId = g.Key, Level = g.Max(e => e.Level) })
            .OrderByDescending(b => b.Level)
            .Take(take)
            .ToListAsync(ct);

        var userIds = bests.Select(b => b.UserId).ToList();
        var names = await db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.UserName ?? "?", ct);

        // Representative run at each player's best level: highest score, then fastest.
        var runs = await db.LeaderboardEntries
            .Where(e => e.Variant == engine.Variant && userIds.Contains(e.UserId))
            .GroupBy(e => new { e.UserId, e.Level })
            .Select(g => new { g.Key.UserId, g.Key.Level, Score = g.Max(e => e.Score), TimeMs = g.Min(e => e.TimeMs) })
            .ToListAsync(ct);
        var runLookup = runs.ToDictionary(r => (r.UserId, r.Level), r => (r.Score, r.TimeMs));

        // Competition ranking by level: players tied on level share a rank.
        int rank = 0;
        int index = 0;
        int? prevLevel = null;
        var rows = bests
            .Select(b =>
            {
                index++;
                if (b.Level != prevLevel)
                {
                    rank = index;
                    prevLevel = b.Level;
                }
                var run = runLookup.GetValueOrDefault((b.UserId, b.Level));
                return new LeaderboardRow(
                    rank,
                    names.GetValueOrDefault(b.UserId, "?"),
                    b.Level,
                    run.Score,
                    run.TimeMs);
            })
            .ToList();

        // The requesting player's rank (null when signed out or unranked).
        int? playerRank = null;
        int? playerBestLevel = null;
        string? userId = userManager.GetUserId(principal);
        if (userId is not null)
        {
            playerRank = await RankOfAsync(db, engine.Variant, userId, ct);
            if (playerRank is not null)
            {
                playerBestLevel = await db.LeaderboardEntries
                    .Where(e => e.Variant == engine.Variant && e.UserId == userId)
                    .MaxAsync(e => (int?)e.Level, ct);
            }
        }

        return Results.Ok(new LeaderboardResponse(engine.Variant, rows, playerRank, playerBestLevel));
    }

    /// <summary>1-based rank by highest level reached, or null if the player has no entries.</summary>
    private static async Task<int?> RankOfAsync(AppDbContext db, string variant, string userId, CancellationToken ct)
    {
        int? myBest = await db.LeaderboardEntries
            .Where(e => e.Variant == variant && e.UserId == userId)
            .MaxAsync(e => (int?)e.Level, ct);
        if (myBest is null)
        {
            return null;
        }

        int better = await db.LeaderboardEntries
            .Where(e => e.Variant == variant)
            .GroupBy(e => e.UserId)
            .Select(g => g.Max(e => e.Level))
            .CountAsync(l => l > myBest.Value, ct);
        return better + 1;
    }
}
