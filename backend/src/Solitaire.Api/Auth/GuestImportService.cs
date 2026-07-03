using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Solitaire.Api.Data;
using Solitaire.Engine;

namespace Solitaire.Api.Auth;

/// <summary>
/// Validates and imports a guest's exported stats/saved games and associates them
/// with a newly-registered account — once (it no-ops if the account already has
/// data). All input is treated as untrusted: variants are allow-listed, counts and
/// ranges are bounded by the DTO attributes, and every saved game is replayed
/// through the authoritative engine to confirm the move sequence is actually legal.
/// </summary>
public sealed class GuestImportService(AppDbContext db)
{
    private static readonly HashSet<string> AllowedStatVariants =
        new(["klondike", "spider", "freecell", "pyramid", "tripeaks"], StringComparer.Ordinal);

    public sealed record ImportResult(bool Ok, string? Error);

    private const int MaxEntries = 16;
    private const int MaxMovesPerSave = 5000;

    public async Task<ImportResult> ImportAsync(string userId, GuestDataDto data, CancellationToken ct)
    {
        // Explicit bounds (untrusted input; nested DataAnnotations are not auto-run).
        if (data.Stats.Count > MaxEntries || data.Saves.Count > MaxEntries)
        {
            return new ImportResult(false, "Too many entries in guest data.");
        }
        foreach (var save in data.Saves)
        {
            if (save.Moves.Count > MaxMovesPerSave || save.Level < 1 || save.HintsUsed < 0)
            {
                return new ImportResult(false, "A saved game exceeded allowed limits.");
            }
        }
        foreach (var stat in data.Stats)
        {
            if (stat.GamesPlayed < 0 || stat.Wins < 0 || stat.BestTimeMs < 0)
            {
                return new ImportResult(false, "Invalid stat values.");
            }
        }

        // "Once": skip if this account already has any imported data.
        var already = await db.PlayerStats.AnyAsync(s => s.UserId == userId, ct)
            || await db.GameSaves.AnyAsync(s => s.UserId == userId, ct);
        if (already)
        {
            return new ImportResult(true, null);
        }

        foreach (var stat in data.Stats)
        {
            if (!AllowedStatVariants.Contains(stat.Variant))
            {
                return new ImportResult(false, $"Unknown variant '{stat.Variant}'.");
            }
            if (stat.Wins > stat.GamesPlayed)
            {
                return new ImportResult(false, "Wins cannot exceed games played.");
            }
        }

        foreach (var save in data.Saves)
        {
            // Only variants with a real engine can be persisted/validated.
            if (!SolitaireEngines.TryGet(save.Variant, out var engine))
            {
                return new ImportResult(false, $"Unsupported save variant '{save.Variant}'.");
            }

            ReplayOutcome outcome;
            try
            {
                outcome = engine.Replay(new GameDefinition(save.Seed, save.Options, save.Moves));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return new ImportResult(false, "A saved game had invalid options or moves.");
            }

            if (!outcome.AllMovesLegal)
            {
                return new ImportResult(false, "A saved game contained an illegal move.");
            }
        }

        // Passed validation → persist.
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        foreach (var stat in data.Stats)
        {
            db.PlayerStats.Add(new PlayerStatEntity
            {
                UserId = userId,
                Variant = stat.Variant,
                GamesPlayed = stat.GamesPlayed,
                Wins = stat.Wins,
                BestTimeMs = stat.BestTimeMs,
            });
        }

        foreach (var save in data.Saves)
        {
            db.GameSaves.Add(new GameSaveEntity
            {
                UserId = userId,
                Variant = save.Variant,
                Level = save.Level,
                Seed = save.Seed,
                OptionsJson = JsonSerializer.Serialize(save.Options, options),
                MovesJson = JsonSerializer.Serialize(save.Moves, options),
                HintsUsed = save.HintsUsed,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
        }

        await db.SaveChangesAsync(ct);
        return new ImportResult(true, null);
    }
}
