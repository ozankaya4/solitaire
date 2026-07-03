using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;

namespace Solitaire.Api.Security;

/// <summary>
/// Localizes the Identity validation errors users actually see during
/// register/login (duplicate username/email, password policy). Error codes are
/// preserved so clients can still branch on them; only descriptions localize.
/// </summary>
public sealed class LocalizedIdentityErrorDescriber(IStringLocalizer<ApiMessages> localizer)
    : IdentityErrorDescriber
{
    private IdentityError Error(string code, string key, params object[] args) =>
        new() { Code = code, Description = localizer[key, args] };

    public override IdentityError DuplicateUserName(string userName) =>
        Error(nameof(DuplicateUserName), "Identity.DuplicateUserName", userName);

    public override IdentityError DuplicateEmail(string email) =>
        Error(nameof(DuplicateEmail), "Identity.DuplicateEmail", email);

    public override IdentityError InvalidUserName(string? userName) =>
        Error(nameof(InvalidUserName), "Identity.InvalidUserName", userName ?? string.Empty);

    public override IdentityError InvalidEmail(string? email) =>
        Error(nameof(InvalidEmail), "Identity.InvalidEmail", email ?? string.Empty);

    public override IdentityError PasswordTooShort(int length) =>
        Error(nameof(PasswordTooShort), "Identity.PasswordTooShort", length);

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        Error(nameof(PasswordRequiresNonAlphanumeric), "Identity.PasswordRequiresNonAlphanumeric");

    public override IdentityError PasswordRequiresDigit() =>
        Error(nameof(PasswordRequiresDigit), "Identity.PasswordRequiresDigit");

    public override IdentityError PasswordRequiresLower() =>
        Error(nameof(PasswordRequiresLower), "Identity.PasswordRequiresLower");

    public override IdentityError PasswordRequiresUpper() =>
        Error(nameof(PasswordRequiresUpper), "Identity.PasswordRequiresUpper");
}
