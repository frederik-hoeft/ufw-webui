using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Ufw.Shared.Web;
using Ufw.Web.Api.V1.Controllers;
using Ufw.Web.Configuration;
using Ufw.Web.Model.V1.Auth;
using Ufw.Web.Services.Auth;

namespace Ufw.Web.Tests.Api.V1;

[TestClass]
public sealed class AuthControllerTests
{
    private const string COOKIE_NAME = "__Host-ufw-refresh";

    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task LoginAsync_WhenAuthenticationFails_ReturnsUnauthorizedWithoutRefreshCookieAsync()
    {
        Mock<IAuthenticationFlowService> authentication = new();
        authentication
            .Setup(service => service.LoginAsync("operator@example.invalid", "wrong-password", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuthenticationTokenResult?)null);
        AuthController controller = CreateController(authentication.Object);

        IActionResult result = await controller.LoginAsync(new LoginRequest("operator@example.invalid", "wrong-password"), TestContext.CancellationToken);

        Assert.IsInstanceOfType<UnauthorizedResult>(result);
        Assert.IsFalse(controller.Response.Headers.ContainsKey("Set-Cookie"));
    }

    [TestMethod]
    public async Task RefreshAsync_WithoutRefreshCookie_ReturnsUnauthorizedWithoutCallingServiceAsync()
    {
        Mock<IAuthenticationFlowService> authentication = new();
        AuthController controller = CreateController(authentication.Object);
        SetCsrfProtectionHeader(controller);

        IActionResult result = await controller.RefreshAsync(TestContext.CancellationToken);

        Assert.IsInstanceOfType<UnauthorizedResult>(result);
        authentication.Verify(
            service => service.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task RefreshAsync_WhenRefreshTokenIsRejected_ReturnsUnauthorizedAndDeletesCookieAsync()
    {
        Mock<IAuthenticationFlowService> authentication = new();
        authentication
            .Setup(service => service.RefreshAsync("stale-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuthenticationTokenResult?)null);
        AuthController controller = CreateController(authentication.Object);
        SetCsrfProtectionHeader(controller);
        controller.Request.Headers.Cookie = $"{COOKIE_NAME}=stale-token";

        IActionResult result = await controller.RefreshAsync(TestContext.CancellationToken);

        Assert.IsInstanceOfType<UnauthorizedResult>(result);
        string setCookie = AssertSingleRefreshCookie(controller);
        StringAssert.Contains(setCookie, "expires=", StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public async Task RefreshAsync_WithoutCsrfProtectionHeader_ReturnsForbiddenWithoutCallingServiceAsync()
    {
        Mock<IAuthenticationFlowService> authentication = new();
        AuthController controller = CreateController(authentication.Object);
        controller.Request.Headers.Cookie = $"{COOKIE_NAME}=refresh-token";

        IActionResult result = await controller.RefreshAsync(TestContext.CancellationToken);

        StatusCodeResult forbidden = Assert.IsInstanceOfType<StatusCodeResult>(result);
        Assert.AreEqual(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        authentication.Verify(service => service.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task LogoutAsync_WithoutCsrfProtectionHeader_ReturnsForbiddenWithoutRevocationAsync()
    {
        Mock<IAuthenticationFlowService> authentication = new();
        AuthController controller = CreateController(authentication.Object);
        controller.Request.Headers.Cookie = $"{COOKIE_NAME}=refresh-token";

        IActionResult result = await controller.LogoutAsync(TestContext.CancellationToken);

        StatusCodeResult forbidden = Assert.IsInstanceOfType<StatusCodeResult>(result);
        Assert.AreEqual(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        authentication.Verify(service => service.RevokeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ChangePasswordAsync_Success_RotatesAuthenticationAsync()
    {
        Mock<IAuthenticationFlowService> authentication = new();
        AccessToken accessToken = new("replacement-access", DateTimeOffset.UtcNow.AddMinutes(5));
        AuthenticationTokenResult tokens = new(accessToken, "replacement-refresh", DateTimeOffset.UtcNow.AddDays(1));
        authentication.Setup(service => service.ChangePasswordAsync("user-id", "current", "replacement", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PasswordChangeResult(IdentityResult.Success, tokens));
        AuthController controller = CreateController(authentication.Object);
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, "user-id")], "test"));

        IActionResult result = await controller.ChangePasswordAsync(new ChangePasswordRequest("current", "replacement"), TestContext.CancellationToken);

        OkObjectResult ok = Assert.IsInstanceOfType<OkObjectResult>(result);
        AuthTokenResponse response = Assert.IsInstanceOfType<AuthTokenResponse>(ok.Value);
        Assert.AreEqual("replacement-access", response.AccessToken);
        StringAssert.Contains(AssertSingleRefreshCookie(controller), "replacement-refresh", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task LogoutAsync_WithoutRefreshCookie_StillDeletesCookieWithoutRevocationAsync()
    {
        Mock<IAuthenticationFlowService> authentication = new();
        AuthController controller = CreateController(authentication.Object);
        SetCsrfProtectionHeader(controller);

        IActionResult result = await controller.LogoutAsync(TestContext.CancellationToken);

        Assert.IsInstanceOfType<NoContentResult>(result);
        authentication.Verify(
            service => service.RevokeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        string setCookie = AssertSingleRefreshCookie(controller);
        StringAssert.Contains(setCookie, "expires=", StringComparison.OrdinalIgnoreCase);
    }

    private static AuthController CreateController(IAuthenticationFlowService authentication) =>
        new(authentication, Options.Create(new RefreshTokenOptions()))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            }
        };

    private static void SetCsrfProtectionHeader(AuthController controller) =>
        controller.Request.Headers[BrowserRequestHeaders.CSRF_PROTECTION] = BrowserRequestHeaders.CSRF_PROTECTION_VALUE;

    private static string AssertSingleRefreshCookie(AuthController controller)
    {
        string?[] setCookies = controller.Response.Headers.SetCookie.ToArray();
        Assert.HasCount(1, setCookies);
        string setCookie = setCookies[0] ?? throw new AssertFailedException("Refresh-token cookie header was null.");
        StringAssert.StartsWith(setCookie, $"{COOKIE_NAME}=", StringComparison.Ordinal);
        return setCookie;
    }
}
