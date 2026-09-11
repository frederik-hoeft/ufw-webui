using Moq;
using Ufw.Client.Api;
using Ufw.Client.Auth;
using Ufw.Client.Tests.Support;

namespace Ufw.Client.Tests.Auth;

[TestClass]
public sealed class AuthenticationServiceTests
{
    private static readonly DateTimeOffset s_now = new(2026, 9, 11, 16, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task GetAccessTokenAsync_FreshTokenDoesNotRefreshAsync()
    {
        TestHost host = new(("fresh", s_now.AddMinutes(2)));

        string? token = await host.Service.GetAccessTokenAsync();

        Assert.AreEqual("fresh", token);
        host.Api.Verify(api => api.TryRefreshAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task GetAccessTokenAsync_ExpiringTokenRefreshesAndUpdatesSessionAsync()
    {
        TestHost host = new(("old", s_now.AddSeconds(20)));
        AuthTokenResponse replacement = new("new", s_now.AddMinutes(5));
        host.Api.Setup(api => api.TryRefreshAsync(It.IsAny<CancellationToken>())).ReturnsAsync(replacement);

        string? token = await host.Service.GetAccessTokenAsync();

        Assert.AreEqual("new", token);
        host.Session.Verify(session => session.SetToken("new", replacement.ExpiresAt), Times.Once);
    }

    [TestMethod]
    public async Task GetAccessTokenAsync_FailedRefreshClearsSessionAsync()
    {
        TestHost host = new(("old", s_now.AddSeconds(20)));
        host.Api.Setup(api => api.TryRefreshAsync(It.IsAny<CancellationToken>())).ReturnsAsync((AuthTokenResponse?)null);

        string? token = await host.Service.GetAccessTokenAsync();

        Assert.IsNull(token);
        host.Session.Verify(session => session.Clear(), Times.Once);
    }

    [TestMethod]
    public async Task RefreshAfterUnauthorizedAsync_NewerFreshTokenWinsWithoutAnotherRefreshAsync()
    {
        TestHost host = new(("replacement", s_now.AddMinutes(1)));

        string? token = await host.Service.RefreshAfterUnauthorizedAsync("rejected");

        Assert.AreEqual("replacement", token);
        host.Api.Verify(api => api.TryRefreshAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task LoginAsync_UsesExclusiveCoordinatorAndStoresReturnedTokenAsync()
    {
        TestHost host = new(null);
        AuthTokenResponse response = new("login-token", s_now.AddMinutes(5));
        host.Api.Setup(api => api.LoginAsync(new LoginRequest("admin@example.invalid", "secret"), It.IsAny<CancellationToken>())).ReturnsAsync(response);

        await host.Service.LoginAsync("admin@example.invalid", "secret");

        Assert.AreEqual(1, host.Coordinator.InvocationCount);
        host.Session.Verify(session => session.SetToken("login-token", response.ExpiresAt), Times.Once);
    }

    [TestMethod]
    public async Task LogoutAsync_ClearsSessionOnlyAfterApiLogoutCompletesAsync()
    {
        TestHost host = new(("token", s_now.AddMinutes(5)));
        host.Api.Setup(api => api.LogoutAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await host.Service.LogoutAsync();

        host.Api.Verify(api => api.LogoutAsync(It.IsAny<CancellationToken>()), Times.Once);
        host.Session.Verify(session => session.Clear(), Times.Once);
    }

    [TestMethod]
    public void InvalidateAccessToken_OnlyDelegatesConditionalClear()
    {
        TestHost host = new(("token", s_now.AddMinutes(5)));

        host.Service.InvalidateAccessToken("rejected");

        host.Session.Verify(session => session.ClearIfCurrent("rejected"), Times.Once);
    }

    private sealed class TestHost
    {
        public TestHost((string AccessToken, DateTimeOffset ExpiresAt)? token)
        {
            Session.SetupGet(session => session.Token).Returns(() => token);
            Coordinator = new InlineAuthenticationOperationCoordinator();
            Service = new AuthenticationService(Api.Object, Session.Object, Coordinator, new MutableTimeProvider(s_now));
        }

        public Mock<IAuthApiClient> Api { get; } = new();

        public Mock<IAuthenticationSession> Session { get; } = new();

        public InlineAuthenticationOperationCoordinator Coordinator { get; }

        public AuthenticationService Service { get; }
    }
}
