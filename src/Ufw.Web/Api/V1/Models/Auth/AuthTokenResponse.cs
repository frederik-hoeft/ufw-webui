namespace Ufw.Web.Api.V1.Models.Auth;

public sealed record AuthTokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
