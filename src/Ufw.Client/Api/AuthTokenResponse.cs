namespace Ufw.Client.Api;

public sealed record AuthTokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
