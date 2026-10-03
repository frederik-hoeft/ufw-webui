namespace Ufw.Web.Services.Auth;

public sealed record PasswordChangeValidationError(PasswordChangeValidationField Field, string ErrorMessage);
