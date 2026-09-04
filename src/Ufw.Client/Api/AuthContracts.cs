using System.ComponentModel.DataAnnotations;

namespace Ufw.Client.Api;

public sealed record LoginRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required] string Password);

public sealed record AuthTokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
