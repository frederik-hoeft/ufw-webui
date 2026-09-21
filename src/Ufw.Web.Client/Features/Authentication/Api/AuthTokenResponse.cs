namespace Ufw.Web.Client.Features.Authentication.Api;

public sealed record AuthTokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
