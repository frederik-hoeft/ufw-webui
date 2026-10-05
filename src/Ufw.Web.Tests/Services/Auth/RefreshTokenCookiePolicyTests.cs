using Microsoft.AspNetCore.Http;
using Ufw.Web.Services.Auth;

namespace Ufw.Web.Tests.Services.Auth;

[TestClass]
public sealed class RefreshTokenCookiePolicyTests
{
    [TestMethod]
    public void Create_UsesHostCookieSecurityPolicy()
    {
        DateTimeOffset expiresAt = new(2026, 10, 3, 18, 0, 0, TimeSpan.Zero);

        CookieOptions options = RefreshTokenCookiePolicy.Create(expiresAt);

        Assert.IsTrue(options.HttpOnly);
        Assert.IsTrue(options.Secure);
        Assert.AreEqual(SameSiteMode.Strict, options.SameSite);
        Assert.AreEqual("/", options.Path);
        Assert.AreEqual(expiresAt, options.Expires);
        Assert.IsTrue(options.IsEssential);
    }

    [TestMethod]
    public void Create_WithoutExpiry_UsesSameDeletePolicy()
    {
        CookieOptions options = RefreshTokenCookiePolicy.Create();

        Assert.IsTrue(options.HttpOnly);
        Assert.IsTrue(options.Secure);
        Assert.AreEqual(SameSiteMode.Strict, options.SameSite);
        Assert.AreEqual("/", options.Path);
        Assert.IsNull(options.Expires);
        Assert.IsTrue(options.IsEssential);
    }
}
