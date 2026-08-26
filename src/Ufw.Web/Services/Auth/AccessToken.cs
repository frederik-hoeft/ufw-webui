namespace Ufw.Web.Services.Auth;

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);
