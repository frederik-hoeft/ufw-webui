using Microsoft.AspNetCore.Http;

namespace Ufw.Web.Services.Auth;

internal static class RefreshTokenCookiePolicy
{
    public static CookieOptions Create(DateTimeOffset? expiresAt = null) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        Expires = expiresAt,
        IsEssential = true,
    };
}
