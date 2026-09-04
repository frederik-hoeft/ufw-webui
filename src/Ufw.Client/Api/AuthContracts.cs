namespace Ufw.Client.Api;

public sealed record LoginRequest(string Email, string Password);

public sealed record AuthTokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
