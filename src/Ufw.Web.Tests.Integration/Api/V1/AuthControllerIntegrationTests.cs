using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ufw.Web.Api.V1.Controllers;
using Ufw.Web.Api.V1.Models.Auth;
using Ufw.Web.Data;
using Ufw.Web.Data.Model;
using Ufw.Web.Services.Auth;
using Ufw.Web.Tests.Integration.Support;

namespace Ufw.Web.Tests.Integration.Api.V1;

[TestClass]
public sealed class AuthControllerIntegrationTests : ControllerIntegrationTest<AuthController>
{
    private const string COOKIE_NAME = "__Host-ufw-refresh";
    private const string EMAIL = "operator@example.invalid";
    private const string PASSWORD = "correct-password";

    public required TestContext TestContext { get; set; }

    [TestMethod]
    public Task LoginAsync_WithValidCredentials_IssuesAccessTokenAndRefreshCookieAsync() =>
        UsingComponentAsync(new LoginRequest(EMAIL, PASSWORD), async (controller, request, serviceProvider, cancellationToken) =>
        {
            DateTimeOffset now = new(2026, 9, 11, 17, 0, 0, TimeSpan.Zero);
            serviceProvider.GetRequiredService<IntegrationTimeProvider>().SetUtcNow(now);
            await CreateUserAsync(serviceProvider, cancellationToken);

            IActionResult result = await controller.LoginAsync(request, cancellationToken);

            OkObjectResult ok = Assert.IsInstanceOfType<OkObjectResult>(result);
            AuthTokenResponse response = Assert.IsInstanceOfType<AuthTokenResponse>(ok.Value);
            Assert.IsFalse(string.IsNullOrWhiteSpace(response.AccessToken));
            Assert.AreEqual(now.AddMinutes(5), response.ExpiresAt);

            string setCookie = AssertSingleRefreshCookie(controller);
            StringAssert.Contains(setCookie, "httponly", StringComparison.OrdinalIgnoreCase);
            StringAssert.Contains(setCookie, "secure", StringComparison.OrdinalIgnoreCase);
            StringAssert.Contains(setCookie, "samesite=strict", StringComparison.OrdinalIgnoreCase);
            StringAssert.Contains(setCookie, "path=/", StringComparison.OrdinalIgnoreCase);

            ApplicationDbContext context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.AreEqual(1, await context.Set<RefreshToken>().CountAsync(cancellationToken));
        }, TestContext.CancellationToken);

    [TestMethod]
    public Task RefreshAsync_WithValidCookie_RotatesRefreshTokenAsync() =>
        UsingComponentAsync(async (controller, serviceProvider, cancellationToken) =>
        {
            IntegrationTimeProvider timeProvider = serviceProvider.GetRequiredService<IntegrationTimeProvider>();
            timeProvider.SetUtcNow(new DateTimeOffset(2026, 9, 11, 17, 0, 0, TimeSpan.Zero));
            await CreateUserAsync(serviceProvider, cancellationToken);

            IAuthenticationFlowService authentication = serviceProvider.GetRequiredService<IAuthenticationFlowService>();
            AuthenticationTokenResult? login = await authentication.LoginAsync(EMAIL, PASSWORD, cancellationToken);
            Assert.IsNotNull(login);
            controller.Request.Headers.Cookie = $"{COOKIE_NAME}={login.RefreshToken}";

            timeProvider.SetUtcNow(timeProvider.GetUtcNow().AddMinutes(1));
            IActionResult result = await controller.RefreshAsync(cancellationToken);

            OkObjectResult ok = Assert.IsInstanceOfType<OkObjectResult>(result);
            AuthTokenResponse response = Assert.IsInstanceOfType<AuthTokenResponse>(ok.Value);
            Assert.AreEqual(timeProvider.GetUtcNow().AddMinutes(5), response.ExpiresAt);
            string replacementToken = GetCookieValue(AssertSingleRefreshCookie(controller));
            Assert.AreNotEqual(login.RefreshToken, replacementToken);

            ApplicationDbContext context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            context.ChangeTracker.Clear();
            List<RefreshToken> tokens = await context.Set<RefreshToken>().ToListAsync(cancellationToken);
            Assert.HasCount(2, tokens);
            RefreshToken revoked = tokens.Single(static token => token.RevokedAt is not null);
            RefreshToken active = tokens.Single(static token => token.RevokedAt is null);
            Assert.AreEqual(revoked.FamilyId, active.FamilyId);
        }, TestContext.CancellationToken);

    [TestMethod]
    public Task LogoutAsync_WithValidCookie_RevokesFamilyAndDeletesCookieAsync() =>
        UsingComponentAsync(async (controller, serviceProvider, cancellationToken) =>
        {
            serviceProvider.GetRequiredService<IntegrationTimeProvider>()
                .SetUtcNow(new DateTimeOffset(2026, 9, 11, 17, 0, 0, TimeSpan.Zero));
            await CreateUserAsync(serviceProvider, cancellationToken);

            IAuthenticationFlowService authentication = serviceProvider.GetRequiredService<IAuthenticationFlowService>();
            AuthenticationTokenResult? login = await authentication.LoginAsync(EMAIL, PASSWORD, cancellationToken);
            Assert.IsNotNull(login);
            controller.Request.Headers.Cookie = $"{COOKIE_NAME}={login.RefreshToken}";

            IActionResult result = await controller.LogoutAsync(cancellationToken);

            Assert.IsInstanceOfType<NoContentResult>(result);
            string setCookie = AssertSingleRefreshCookie(controller);
            StringAssert.Contains(setCookie, "expires=", StringComparison.OrdinalIgnoreCase);

            ApplicationDbContext context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            context.ChangeTracker.Clear();
            Assert.AreEqual(0, await context.Set<RefreshToken>().CountAsync(static token => token.RevokedAt == null, cancellationToken));
        }, TestContext.CancellationToken);

    private static async Task CreateUserAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        UserManager<IdentityUser> userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();
        IdentityUser user = new()
        {
            UserName = EMAIL,
            Email = EMAIL,
            EmailConfirmed = true,
            LockoutEnabled = true,
        };
        IdentityResult creationResult = await userManager.CreateAsync(user, PASSWORD);
        Assert.IsTrue(creationResult.Succeeded, string.Join("; ", creationResult.Errors.Select(static error => error.Description)));
    }

    private static string AssertSingleRefreshCookie(AuthController controller)
    {
        string?[] setCookies = controller.Response.Headers.SetCookie.ToArray();
        Assert.HasCount(1, setCookies);
        string setCookie = setCookies[0] ?? throw new AssertFailedException("Refresh-token cookie header was null.");
        StringAssert.StartsWith(setCookie, $"{COOKIE_NAME}=", StringComparison.Ordinal);
        return setCookie;
    }

    private static string GetCookieValue(string setCookie)
    {
        int valueStart = COOKIE_NAME.Length + 1;
        int valueEnd = setCookie.IndexOf(';', valueStart);
        return setCookie[valueStart..(valueEnd < 0 ? setCookie.Length : valueEnd)];
    }
}
