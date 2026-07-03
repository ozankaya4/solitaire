using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Solitaire.Api.Data;

namespace Solitaire.Api.Account;

/// <summary>Confirmation payload for the destructive account-deletion endpoint.</summary>
public sealed class DeleteAccountRequest
{
    /// <summary>Must exactly match the account's username — an explicit, deliberate confirmation.</summary>
    [Required(ErrorMessage = "Validation.Required")]
    public string ConfirmUsername { get; set; } = string.Empty;
}

/// <summary>
/// KVKK/GDPR data-subject endpoints: export (right of access / data portability)
/// and delete (right to erasure). Both require authentication; delete additionally
/// requires anti-forgery and an explicit username confirmation, and cascade-removes
/// every record tied to the account.
/// </summary>
public static class AccountEndpoints
{
    private static readonly JsonSerializerOptions ExportJson =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static void MapAccountEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/account").RequireAuthorization();
        group.MapGet("/export", ExportAsync);
        group.MapPost("/delete", DeleteAsync);
    }

    private static async Task<IResult> ExportAsync(
        UserManager<ApplicationUser> userManager,
        AppDbContext db,
        ClaimsPrincipal principal,
        CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var stats = await db.PlayerStats
            .Where(s => s.UserId == user.Id)
            .Select(s => new { s.Variant, s.GamesPlayed, s.Wins, s.BestTimeMs })
            .ToListAsync(ct);

        var saves = await db.GameSaves
            .Where(s => s.UserId == user.Id)
            .Select(s => new { s.Variant, s.Level, s.Seed, s.OptionsJson, s.MovesJson, s.HintsUsed, s.UpdatedAt })
            .ToListAsync(ct);

        var leaderboard = await db.LeaderboardEntries
            .Where(e => e.UserId == user.Id)
            .Select(e => new { e.Variant, e.Seed, e.Score, e.TimeMs, e.MoveCount, e.CreatedAt })
            .ToListAsync(ct);

        var package = new
        {
            exportedAt = DateTimeOffset.UtcNow,
            account = new { user.Id, user.UserName, user.Email, user.CreatedAt },
            stats,
            saves,
            leaderboard,
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(package, ExportJson);
        return Results.File(bytes, "application/json", "solitaire-data-export.json");
    }

    private static async Task<IResult> DeleteAsync(
        DeleteAccountRequest request,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        AppDbContext db,
        IAntiforgery antiforgery,
        IStringLocalizer<ApiMessages> localizer,
        ClaimsPrincipal principal,
        HttpContext http,
        CancellationToken ct)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(http);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.BadRequest(new { error = localizer["Auth.InvalidAntiForgery"].Value });
        }

        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        // Explicit, deliberate confirmation before an irreversible deletion.
        if (!string.Equals(request.ConfirmUsername, user.UserName, StringComparison.Ordinal))
        {
            return Results.BadRequest(new { error = localizer["Account.ConfirmMismatch"].Value });
        }

        // Cascade-delete every associated record, then the account itself. The FKs
        // are ON DELETE CASCADE too; doing it explicitly is transparent and
        // provider-independent.
        await db.LeaderboardEntries.Where(e => e.UserId == user.Id).ExecuteDeleteAsync(ct);
        await db.GameSaves.Where(s => s.UserId == user.Id).ExecuteDeleteAsync(ct);
        await db.PlayerStats.Where(s => s.UserId == user.Id).ExecuteDeleteAsync(ct);

        var result = await userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Account deletion failed.");
        }

        await signInManager.SignOutAsync();
        return Results.Ok(new { deleted = true });
    }
}
