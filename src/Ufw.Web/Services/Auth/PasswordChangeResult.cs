namespace Ufw.Web.Services.Auth;

public sealed record PasswordChangeResult(IReadOnlyList<PasswordChangeValidationError> ValidationErrors, AuthenticationTokenResult? Authentication)
{
    public bool Succeeded => ValidationErrors.Count == 0;
}
