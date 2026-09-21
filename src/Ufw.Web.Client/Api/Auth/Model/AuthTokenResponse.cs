namespace Ufw.Web.Client.Api.Auth.Model;

public sealed record AuthTokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
