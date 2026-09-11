using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Ufw.Web.Configuration;
using Ufw.Web.Services.Auth;

namespace Ufw.Web.Tests.Services.Auth;

[TestClass]
public sealed class JwtTokenServiceTests
{
    private static readonly DateTimeOffset s_now = new(2026, 9, 11, 17, 30, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task IssueAsync_UsesConfiguredLifetimeIdentityAndRolesAsync()
    {
        IdentityUser user = new()
        {
            Id = "user-id",
            Email = "admin@example.invalid",
            UserName = "Administrator",
        };
        Mock<UserManager<IdentityUser>> userManager = CreateUserManager();
        userManager.Setup(manager => manager.GetRolesAsync(user)).ReturnsAsync(["admin", "operator"]);
        JwtTokenService service = CreateService(userManager.Object, TimeSpan.FromMinutes(10));

        AccessToken accessToken = await service.IssueAsync(user);
        JwtSecurityToken token = new JwtSecurityTokenHandler().ReadJwtToken(accessToken.Value);

        Assert.AreEqual(s_now.AddMinutes(10), accessToken.ExpiresAt);
        Assert.AreEqual("ufw-webui", token.Issuer);
        CollectionAssert.Contains(token.Audiences.ToArray(), "ufw-webui-client");
        Assert.AreEqual("user-id", token.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.AreEqual("admin@example.invalid", token.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.AreEqual("Administrator", token.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Name).Value);
        CollectionAssert.AreEquivalent(new[] { "admin", "operator" }, token.Claims.Where(claim => claim.Type == "role").Select(claim => claim.Value).ToArray());
        Assert.IsFalse(string.IsNullOrWhiteSpace(token.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Jti).Value));
        Assert.AreEqual(s_now.UtcDateTime, token.ValidFrom);
        Assert.AreEqual(accessToken.ExpiresAt.UtcDateTime, token.ValidTo);
    }

    [TestMethod]
    public async Task IssueAsync_OmitsBlankOptionalIdentityClaimsAsync()
    {
        IdentityUser user = new() { Id = "user-id", Email = " ", UserName = null };
        Mock<UserManager<IdentityUser>> userManager = CreateUserManager();
        userManager.Setup(manager => manager.GetRolesAsync(user)).ReturnsAsync([]);
        JwtTokenService service = CreateService(userManager.Object, TimeSpan.FromMinutes(5));

        AccessToken accessToken = await service.IssueAsync(user);
        JwtSecurityToken token = new JwtSecurityTokenHandler().ReadJwtToken(accessToken.Value);

        Assert.IsFalse(token.Claims.Any(claim => claim.Type == JwtRegisteredClaimNames.Email));
        Assert.IsFalse(token.Claims.Any(claim => claim.Type == JwtRegisteredClaimNames.Name));
        Assert.IsFalse(token.Claims.Any(claim => claim.Type == "role"));
    }

    [TestMethod]
    public async Task IssueAsync_PreCanceledRequestDoesNotQueryIdentityStoreAsync()
    {
        Mock<UserManager<IdentityUser>> userManager = CreateUserManager();
        JwtTokenService service = CreateService(userManager.Object, TimeSpan.FromMinutes(5));
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.IssueAsync(new IdentityUser { Id = "user-id" }, cancellation.Token));

        userManager.Verify(manager => manager.GetRolesAsync(It.IsAny<IdentityUser>()), Times.Never);
    }

    private static JwtTokenService CreateService(UserManager<IdentityUser> userManager, TimeSpan lifetime)
    {
        IJwtSigningKeyProvider signing = new TestSigningKeyProvider();
        JwtOptions options = new()
        {
            Issuer = "ufw-webui",
            Audience = "ufw-webui-client",
            AccessTokenLifetime = lifetime,
        };
        return new JwtTokenService(userManager, signing, Options.Create(options), new FixedTimeProvider(s_now));
    }

    private static Mock<UserManager<IdentityUser>> CreateUserManager()
    {
        Mock<IUserStore<IdentityUser>> store = new();
        return new Mock<UserManager<IdentityUser>>(
            store.Object,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<IdentityUser>(),
            Array.Empty<IUserValidator<IdentityUser>>(),
            Array.Empty<IPasswordValidator<IdentityUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new Mock<IServiceProvider>().Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<UserManager<IdentityUser>>.Instance);
    }

    private sealed class TestSigningKeyProvider : IJwtSigningKeyProvider
    {
        public SecurityKey SigningKey { get; } = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef"));

        public string SigningAlgorithm => SecurityAlgorithms.HmacSha256;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
