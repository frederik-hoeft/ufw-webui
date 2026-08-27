using Microsoft.AspNetCore.Identity;

namespace Ufw.Web.Services.Auth;

public sealed record RefreshTokenRotationResult(IdentityUser User, string Token, DateTimeOffset ExpiresAt);
