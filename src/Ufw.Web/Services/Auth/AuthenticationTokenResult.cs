namespace Ufw.Web.Services.Auth;

public sealed record AuthenticationTokenResult(AccessToken AccessToken, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);
