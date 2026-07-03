using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
        ClaimsPrincipal principal,
        CancellationToken ct)
    {
        if (!TryValidate(request, out var errors))
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
                result.Entry!.Score,
                result.Entry.TimeMs,
                await RankOfAsync(db, result.Entry.Variant, userId, ct) ?? 0)),
            GameVerificationService.Verdict.Duplicate => Results.Conflict(
                new { error = "This game has already been submitted." }),
            // The submission was understood but failed verification — say only that.
            _ => Results.UnprocessableEntity(new { error = "Submission failed verification." }),
        };
    }

    private static async Task<IResult> GetLeaderboardAsync(
        string variant,
        int? top,
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        ClaimsPrincipal principal,
        CancellationToken ct)
    {
        if (!SolitaireEngines.TryGet(variant, out var engine))
        {
            return Results.NotFound(new { error = $"Unknown variant '{variant}'." });
        }

        int take = Math.Clamp(top ?? 10, 1, MaxTop);

        // Best score per player, ranked.
        var bests = await db.LeaderboardEntries
            .Where(e => e.Variant == engine.Variant)
            .GroupBy(e => e.UserId)
            .Select(g => new { UserId = g.Key, Score = g.Max(e => e.Score) })
            .OrderByDescending(b => b.Score)
            .Take(take)
            .ToListAsync(ct);

        var userIds = bests.Select(b => b.UserId).ToList();
        var names = await db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.UserName ?? "?", ct);

        // Representative time: the fastest run at that player's best score.
        var times = await db.LeaderboardEntries
            .Where(e => e.Variant == engine.Variant && userIds.Contains(e.UserId))
            .GroupBy(e => new { e.UserId, e.Score })
            .Select(g => new { g.Key.UserId, g.Key.Score, TimeMs = g.Min(e => e.TimeMs) })
            .ToListAsync(ct);
        var timeLookup = times.ToDictionary(t => (t.UserId, t.Score), t => t.TimeMs);

        var rows = bests
            .Select((b, i) => new LeaderboardRow(
                i + 1,
                names.GetValueOrDefault(b.UserId, "?"),
                b.Score,
                timeLookup.GetValueOrDefault((b.UserId, b.Score), 0)))
            .ToList();

        // The requesting player's rank (null when signed out or unranked).
        int? playerRank = null;
        int? playerBest = null;
        string? userId = userManager.GetUserId(principal);
        if (userId is not null)
        {
            playerRank = await RankOfAsync(db, engine.Variant, userId, ct);
            if (playerRank is not null)
            {
                playerBest = await db.LeaderboardEntries
                    .Where(e => e.Variant == engine.Variant && e.UserId == userId)
                    .MaxAsync(e => (int?)e.Score, ct);
            }
        }

        return Results.Ok(new LeaderboardResponse(engine.Variant, rows, playerRank, playerBest));
    }

    /// <summary>1-based rank by best score, or null if the player has no entries.</summary>
    private static async Task<int?> RankOfAsync(AppDbContext db, string variant, string userId, CancellationToken ct)
    {
        int? myBest = await db.LeaderboardEntries
            .Where(e => e.Variant == variant && e.UserId == userId)
            .MaxAsync(e => (int?)e.Score, ct);
        if (myBest is null)
        {
            return null;
        }

        int better = await db.LeaderboardEntries
            .Where(e => e.Variant == variant)
            .GroupBy(e => e.UserId)
            .Select(g => g.Max(e => e.Score))
            .CountAsync(s => s > myBest.Value, ct);
        return better + 1;
    }

    private static bool TryValidate(object model, out Dictionary<string, string[]> errors)
    {
        var results = new List<ValidationResult>();
        var ok = Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        errors = results
            .SelectMany(r => (r.MemberNames.Any() ? r.MemberNames : [""]).Select(m => (Member: m, r.ErrorMessage)))
            .GroupBy(x => x.Member, x => x.ErrorMessage ?? "Invalid.")
            .ToDictionary(g => g.Key, g => g.ToArray());
        return ok;
    }
}
