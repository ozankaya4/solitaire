using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Solitaire.Api.Data;

namespace Solitaire.Api.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/auth");

        // Registration — rate-limited, validated, atomic with guest import.
        group
            .MapPost("/register", RegisterAsync)
            .RequireRateLimiting("auth")
            .AllowAnonymous();

        // Login — rate-limited; lockout handled by Identity.
        group
            .MapPost("/login", LoginAsync)
            .RequireRateLimiting("auth")
            .AllowAnonymous();

        // Logout — authenticated + anti-forgery validated (state-changing).
        group.MapPost("/logout", LogoutAsync).RequireAuthorization();

        // Current user — the canonical "is this request authenticated?" probe.
        group.MapGet("/me", MeAsync).RequireAuthorization();

        // Issues an anti-forgery token (double-submit cookie) for the SPA.
        group.MapGet("/csrf", (IAntiforgery antiforgery, HttpContext http) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(http);
            return Results.Ok(new { token = tokens.RequestToken });
        });
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        GuestImportService guestImport,
        CancellationToken ct)
    {
        if (!TryValidate(request, out var errors))
        {
            return Results.ValidationProblem(errors);
        }

        var user = new ApplicationUser { UserName = request.Username, Email = request.Email };
        var created = await userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            return Results.ValidationProblem(ToErrors(created));
        }

        if (request.GuestData is not null)
        {
            var import = await guestImport.ImportAsync(user.Id, request.GuestData, ct);
            if (!import.Ok)
            {
                // Keep registration atomic: undo the account if the payload was bad.
                await userManager.DeleteAsync(user);
                return Results.ValidationProblem(
                    new Dictionary<string, string[]> { ["guestData"] = [import.Error ?? "Invalid guest data."] });
            }
        }

        await signInManager.SignInAsync(user, isPersistent: true);
        return Results.Ok(new UserResponse(user.Id, user.UserName, user.Email));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        if (!TryValidate(request, out var errors))
        {
            return Results.ValidationProblem(errors);
        }

        var user =
            await userManager.FindByNameAsync(request.UsernameOrEmail)
            ?? await userManager.FindByEmailAsync(request.UsernameOrEmail);
        if (user is null)
        {
            return Results.Unauthorized(); // generic — do not reveal which field was wrong
        }

        var result = await signInManager.PasswordSignInAsync(
            user, request.Password, request.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            return Results.Ok(new UserResponse(user.Id, user.UserName, user.Email));
        }
        if (result.IsLockedOut)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status423Locked,
                title: "Account temporarily locked due to repeated failed attempts.");
        }
        return Results.Unauthorized();
    }

    private static async Task<IResult> LogoutAsync(
        SignInManager<ApplicationUser> signInManager,
        IAntiforgery antiforgery,
        HttpContext http)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(http);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.BadRequest(new { error = "Invalid anti-forgery token." });
        }

        await signInManager.SignOutAsync();
        return Results.Ok();
    }

    private static async Task<IResult> MeAsync(
        UserManager<ApplicationUser> userManager,
        ClaimsPrincipal principal)
    {
        var user = await userManager.GetUserAsync(principal);
        return user is null
            ? Results.Unauthorized()
            : Results.Ok(new UserResponse(user.Id, user.UserName, user.Email));
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

    private static Dictionary<string, string[]> ToErrors(IdentityResult result) =>
        result.Errors
            .GroupBy(e => e.Code)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
}
