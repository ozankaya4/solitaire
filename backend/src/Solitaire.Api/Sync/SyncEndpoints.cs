using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Solitaire.Api.Auth;
using Solitaire.Api.Data;
using Solitaire.Engine;

namespace Solitaire.Api.Sync;

/// <summary>
/// Cross-device sync of resumable games and per-variant progress for signed-in
/// players. Local-first: the client owns the data and pushes it up; the server is
/// a durable mirror. Untrusted like everything else — saved games are replayed
/// through the authoritative engine before they are stored, and progress only
/// ever moves forward (the server keeps the max so a stale device can't undo it).
/// </summary>
public static class SyncEndpoints
{
    private const int MaxSavesPerUser = 8;
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static void MapSyncEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/sync").RequireAuthorization().RequireRateLimiting("sync");

        // Pull everything this account has (a device calls this right after login).
        group.MapGet("", GetStateAsync);
        // Push one resumable game (upsert, newest-wins) or clear it.
        group.MapPut("/saves/{variant}", PutSaveAsync);
        group.MapDelete("/saves/{variant}", DeleteSaveAsync);
        // Push current level for a variant (kept monotonic server-side).
        group.MapPut("/progress/{variant}", PutProgressAsync);
        // Push lifetime stats for a variant (merged monotonically server-side).
        group.MapPut("/stats/{variant}", PutStatsAsync);
    }

    private static async Task<IResult> GetStateAsync(
        UserManager<ApplicationUser> userManager,
        AppDbContext db,
        ClaimsPrincipal principal,
        CancellationToken ct)
    {
        var userId = userManager.GetUserId(principal);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var saves = await db.GameSaves
            .Where(s => s.UserId == userId)
            .ToListAsync(ct);
        // The PlayerStats row carries both the level (progress) and the lifetime
        // stats for a variant, so one read feeds both lists.
        var playerStats = await db.PlayerStats
            .Where(s => s.UserId == userId)
            .ToListAsync(ct);

        var progress = playerStats
            .Select(s => new ProgressDto { Variant = s.Variant, CurrentLevel = s.CurrentLevel })
            .ToList();
        var stats = playerStats
            .Select(s => new StatsDto
            {
                Variant = s.Variant,
                GamesPlayed = s.GamesPlayed,
                Wins = s.Wins,
                BestTimeMs = s.BestTimeMs,
            })
            .ToList();

        return Results.Ok(new SyncStateResponse(saves.Select(ToDto).ToList(), progress, stats));
    }

    private static async Task<IResult> PutSaveAsync(
        string variant,
        SaveDto request,
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
        // The route is the source of truth for which variant this is.
        request.Variant = variant;

        // Only variants with a real engine can be validated + stored.
        if (!SolitaireEngines.TryGet(variant, out var engine))
        {
            return Results.NotFound(new { error = localizer["Leaderboard.UnknownVariant", variant].Value });
        }

        // Replay: the move log must be legal for this deal (but need not be a win —
        // this is an in-progress game).
        try
        {
            var outcome = engine.Replay(new GameDefinition(request.Seed, request.Options, request.Moves));
            if (!outcome.AllMovesLegal)
            {
                return Results.UnprocessableEntity(new { error = localizer["Sync.IllegalSave"].Value });
            }
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            return Results.UnprocessableEntity(new { error = localizer["Sync.IllegalSave"].Value });
        }

        var existing = await db.GameSaves
            .SingleOrDefaultAsync(s => s.UserId == userManager.GetUserId(principal) && s.Variant == engine.Variant, ct);
        var updatedAt = DateTimeOffset.FromUnixTimeMilliseconds(request.UpdatedAt);

        if (existing is null)
        {
            // Enforce the per-account cap (one save per variant, so this bounds variants).
            var userId = userManager.GetUserId(principal)!;
            int count = await db.GameSaves.CountAsync(s => s.UserId == userId, ct);
            if (count >= MaxSavesPerUser)
            {
                return Results.Conflict(new { error = localizer["Sync.TooManySaves"].Value });
            }
            db.GameSaves.Add(Apply(new GameSaveEntity { UserId = userId, Variant = engine.Variant }, request, updatedAt));
        }
        else if (updatedAt >= existing.UpdatedAt)
        {
            // Newest write wins; an older device's push is ignored (idempotent).
            Apply(existing, request, updatedAt);
        }

        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteSaveAsync(
        string variant,
        UserManager<ApplicationUser> userManager,
        AppDbContext db,
        ClaimsPrincipal principal,
        CancellationToken ct)
    {
        var userId = userManager.GetUserId(principal);
        if (userId is null)
        {
            return Results.Unauthorized();
        }
        await db.GameSaves
            .Where(s => s.UserId == userId && s.Variant == variant)
            .ExecuteDeleteAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> PutProgressAsync(
        string variant,
        ProgressDto request,
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
        if (!SolitaireEngines.TryGet(variant, out var engine))
        {
            return Results.NotFound(new { error = localizer["Leaderboard.UnknownVariant", variant].Value });
        }

        var userId = userManager.GetUserId(principal)!;
        var stat = await db.PlayerStats.SingleOrDefaultAsync(s => s.UserId == userId && s.Variant == engine.Variant, ct);
        if (stat is null)
        {
            stat = new PlayerStatEntity { UserId = userId, Variant = engine.Variant, CurrentLevel = 1 };
            db.PlayerStats.Add(stat);
        }
        // Monotonic: progress only moves forward, so a stale device can't roll it back.
        stat.CurrentLevel = Math.Max(stat.CurrentLevel, request.CurrentLevel);

        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> PutStatsAsync(
        string variant,
        StatsDto request,
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
        if (!SolitaireEngines.TryGet(variant, out var engine))
        {
            return Results.NotFound(new { error = localizer["Leaderboard.UnknownVariant", variant].Value });
        }

        var userId = userManager.GetUserId(principal)!;
        var stat = await db.PlayerStats.SingleOrDefaultAsync(s => s.UserId == userId && s.Variant == engine.Variant, ct);
        if (stat is null)
        {
            stat = new PlayerStatEntity { UserId = userId, Variant = engine.Variant, CurrentLevel = 1 };
            db.PlayerStats.Add(stat);
        }

        // Monotonic, idempotent merge: counters only climb (highest wins), the best
        // time only drops. Re-pushing the same values never double-counts, and a
        // stale device can't undo another device's progress.
        stat.GamesPlayed = Math.Max(stat.GamesPlayed, request.GamesPlayed);
        stat.Wins = Math.Max(stat.Wins, request.Wins);
        if (request.BestTimeMs is { } incoming)
        {
            stat.BestTimeMs = stat.BestTimeMs is { } current ? Math.Min(current, incoming) : incoming;
        }

        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static GameSaveEntity Apply(GameSaveEntity entity, SaveDto dto, DateTimeOffset updatedAt)
    {
        entity.Level = dto.Level;
        entity.Seed = dto.Seed;
        entity.OptionsJson = JsonSerializer.Serialize(dto.Options, Web);
        entity.MovesJson = JsonSerializer.Serialize(dto.Moves, Web);
        entity.HintsUsed = dto.HintsUsed;
        entity.ElapsedMs = dto.ElapsedMs;
        entity.UpdatedAt = updatedAt;
        return entity;
    }

    private static SaveDto ToDto(GameSaveEntity e) => new()
    {
        Variant = e.Variant,
        Level = e.Level,
        Seed = e.Seed,
        Options = JsonSerializer.Deserialize<Dictionary<string, int>>(e.OptionsJson, Web) ?? [],
        Moves = JsonSerializer.Deserialize<List<MoveDto>>(e.MovesJson, Web) ?? [],
        HintsUsed = e.HintsUsed,
        ElapsedMs = e.ElapsedMs,
        UpdatedAt = e.UpdatedAt.ToUnixTimeMilliseconds(),
    };
}
