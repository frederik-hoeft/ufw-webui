namespace Ufw.Web.Services.Auth;

public sealed record RefreshTokenIssueResult(string Token, DateTimeOffset ExpiresAt);
