namespace Ufw.Web.Data.Access.Auth;

internal sealed record RefreshTokenIssueResult(string Token, DateTimeOffset ExpiresAt);
