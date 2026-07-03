using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Solitaire.Api.Data;
using Solitaire.Engine;

namespace Solitaire.Api.Leaderboard;

/// <summary>
/// The anti-cheat core. Never trusts the client: the submitted move log is
/// replayed through the authoritative engine and the result is recomputed
/// server-side. Only games whose replay produces a legal, completed win with the
/// exact claimed score (and a plausible duration) are recorded.
/// </summary>
public sealed partial class GameVerificationService(
    AppDbContext db,
    ILogger<GameVerificationService> logger)
{
    /// <summary>
    /// Minimum plausible milliseconds per move. A human cannot sustain faster than
    /// ~10 moves/second across a whole game; anything quicker is rejected.
    /// </summary>
    public const long MinMsPerMove = 100;

    /// <summary>Upper bound on a single game's duration (24h).</summary>
    public const long MaxGameMs = 24L * 60 * 60 * 1000;

    public enum Verdict
    {
        Accepted = 0,
        UnknownVariant,
        InvalidOptions,
        IllegalMove,
        NotAWin,
        ScoreMismatch,
        ImplausibleTime,
        Duplicate,
    }

    public sealed record Result(Verdict Verdict, LeaderboardEntryEntity? Entry, string? Detail);

    public async Task<Result> VerifyAndRecordAsync(string userId, SubmitGameRequest request, CancellationToken ct)
    {
        // 1) The variant must have a server-side engine.
        if (!SolitaireEngines.TryGet(request.Variant, out var engine))
        {
            return Reject(userId, request, Verdict.UnknownVariant, "no engine for variant");
        }

        // 2) Plausibility: the claimed time cannot beat the minimum possible pace.
        long minPlausible = request.Moves.Count * MinMsPerMove;
        if (request.ClaimedTimeMs < minPlausible || request.ClaimedTimeMs > MaxGameMs)
        {
            return Reject(
                userId,
                request,
                Verdict.ImplausibleTime,
                $"claimed {request.ClaimedTimeMs}ms for {request.Moves.Count} moves (min {minPlausible}ms)");
        }

        // 3) Independent replay — the heart of the verification.
        ReplayOutcome outcome;
        try
        {
            outcome = engine.Replay(new GameDefinition(request.Seed, request.Options, request.Moves));
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            return Reject(userId, request, Verdict.InvalidOptions, ex.Message);
        }

        if (!outcome.AllMovesLegal)
        {
            return Reject(
                userId,
                request,
                Verdict.IllegalMove,
                $"illegal move at index {outcome.FirstIllegalMoveIndex}");
        }

        if (!outcome.Won)
        {
            return Reject(userId, request, Verdict.NotAWin, "replay did not finish as a win");
        }

        // 4) The recomputed score is authoritative; the claim must match it exactly.
        if (outcome.Score != request.ClaimedScore)
        {
            return Reject(
                userId,
                request,
                Verdict.ScoreMismatch,
                $"replay score {outcome.Score} != claimed {request.ClaimedScore}");
        }

        // 5) Dedupe: the same game (variant+seed+options+moves) counts once per user.
        string hash = CanonicalHash(request);
        bool duplicate = await db.LeaderboardEntries
            .AnyAsync(e => e.UserId == userId && e.GameHash == hash, ct);
        if (duplicate)
        {
            return Reject(userId, request, Verdict.Duplicate, "already recorded");
        }

        // 6) Record the verified result + update lifetime stats.
        var entry = new LeaderboardEntryEntity
        {
            UserId = userId,
            Variant = engine.Variant, // canonical lowercase id
            Seed = request.Seed,
            OptionsJson = CanonicalOptionsJson(request.Options),
            Score = outcome.Score,
            TimeMs = request.ClaimedTimeMs,
            MoveCount = request.Moves.Count,
            GameHash = hash,
        };
        db.LeaderboardEntries.Add(entry);

        var stat = await db.PlayerStats
            .SingleOrDefaultAsync(s => s.UserId == userId && s.Variant == engine.Variant, ct);
        if (stat is null)
        {
            stat = new PlayerStatEntity { UserId = userId, Variant = engine.Variant };
            db.PlayerStats.Add(stat);
        }
        stat.GamesPlayed += 1;
        stat.Wins += 1;
        stat.BestTimeMs = stat.BestTimeMs is null
            ? request.ClaimedTimeMs
            : Math.Min(stat.BestTimeMs.Value, request.ClaimedTimeMs);

        await db.SaveChangesAsync(ct);
        return new Result(Verdict.Accepted, entry, null);
    }

    private Result Reject(string userId, SubmitGameRequest request, Verdict verdict, string detail)
    {
        // Monitoring log: internal user id only — no email/username/PII, no move log.
        LogRejected(logger, verdict, userId, request.Variant, request.Seed, detail);
        return new Result(verdict, null, detail);
    }

    /// <summary>Canonical, order-independent hash identifying a submitted game.</summary>
    internal static string CanonicalHash(SubmitGameRequest request)
    {
        var sb = new StringBuilder();
        sb.Append(request.Variant.ToLowerInvariant()).Append('|');
        sb.Append(request.Seed.ToString(CultureInfo.InvariantCulture)).Append('|');
        sb.Append(CanonicalOptionsJson(request.Options)).Append('|');
        foreach (var move in request.Moves)
        {
            sb.Append(move.Type).Append(',')
                .Append(move.Source?.ToString(CultureInfo.InvariantCulture) ?? "_").Append(',')
                .Append(move.Destination?.ToString(CultureInfo.InvariantCulture) ?? "_").Append(',')
                .Append(move.Count?.ToString(CultureInfo.InvariantCulture) ?? "_").Append(';');
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    private static string CanonicalOptionsJson(Dictionary<string, int> options) =>
        JsonSerializer.Serialize(options.OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .ToDictionary(kv => kv.Key, kv => kv.Value));

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Game submission rejected: {Verdict} (user {UserId}, variant {Variant}, seed {Seed}) — {Detail}")]
    private static partial void LogRejected(
        ILogger logger, Verdict verdict, string userId, string variant, int seed, string detail);
}
