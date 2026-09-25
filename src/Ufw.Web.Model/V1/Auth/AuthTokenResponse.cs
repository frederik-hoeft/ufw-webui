namespace Ufw.Web.Model.V1.Auth;

public sealed record AuthTokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
