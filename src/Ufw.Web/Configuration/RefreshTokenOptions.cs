namespace Ufw.Web.Configuration;

public sealed class RefreshTokenOptions
{
    public const string SECTION_NAME = "Auth:RefreshToken";

    public string CookieName { get; set; } = "__Host-ufw-refresh";

    public TimeSpan Lifetime { get; set; } = TimeSpan.FromDays(30);
}
