using Microsoft.AspNetCore.Identity;

namespace Ufw.Web.Services.Auth;

internal static class IdentityPasswordChangeErrorMapper
{
    public static IReadOnlyList<PasswordChangeValidationError> Map(IEnumerable<IdentityError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        return [.. errors.Select(static error => new PasswordChangeValidationError(MapField(error.Code), error.Description))];
    }

    private static PasswordChangeValidationField MapField(string code) => code switch
    {
        "PasswordMismatch" => PasswordChangeValidationField.CurrentPassword,
        "PasswordRequiresDigit" or
        "PasswordRequiresLower" or
        "PasswordRequiresNonAlphanumeric" or
        "PasswordRequiresUniqueChars" or
        "PasswordRequiresUpper" or
        "PasswordTooShort" => PasswordChangeValidationField.NewPassword,

        // Preserve the previous API fallback for provider-specific Identity errors while making
        // that fallback explicit rather than coupling the controller to arbitrary error codes.
        _ => PasswordChangeValidationField.NewPassword,
    };
}
