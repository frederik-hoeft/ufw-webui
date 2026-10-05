namespace Ufw.Web.Data.Access.Auth;

internal sealed record RefreshTokenRotationResult(string UserId, string Token, DateTimeOffset ExpiresAt);
