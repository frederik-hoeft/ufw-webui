using Microsoft.AspNetCore.Components.Authorization;
using Moq;
using System.Security.Claims;
using Ufw.Client.Api;
using Ufw.Client.Auth;

namespace Ufw.Client.Tests.Auth;

[TestClass]
public sealed class AuthenticationSessionTests
{
    [TestMethod]
    public async Task SetToken_UpdatesTokenStateAndNotifiesAsync()
    {
        ClaimsPrincipal principal = new(new ClaimsIdentity([new Claim(ClaimTypes.Name, "admin")], "Bearer"));
        Mock<IAccessTokenPrincipalFactory> factory = new();
        factory.Setup(service => service.CreatePrincipal("token")).Returns(principal);
        AuthenticationSession session = new(factory.Object);
        int notifications = 0;
        session.AuthenticationStateChanged += _ => notifications++;
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddMinutes(5);

        session.SetToken("token", expiresAt);
        AuthenticationState state = await session.GetAuthenticationStateAsync();

        Assert.AreEqual(("token", expiresAt), session.Token);
        Assert.AreSame(principal, state.User);
        Assert.AreEqual(1, notifications);
    }

    [TestMethod]
    public async Task Clear_IsIdempotentAndRestoresAnonymousStateAsync()
    {
        ClaimsPrincipal principal = new(new ClaimsIdentity(authenticationType: "Bearer"));
        Mock<IAccessTokenPrincipalFactory> factory = new();
        factory.Setup(service => service.CreatePrincipal(It.IsAny<string>())).Returns(principal);
        AuthenticationSession session = new(factory.Object);
        int notifications = 0;
        session.AuthenticationStateChanged += _ => notifications++;
        session.SetToken("token", DateTimeOffset.UtcNow.AddMinutes(5));

        session.Clear();
        session.Clear();
        AuthenticationState state = await session.GetAuthenticationStateAsync();

        Assert.IsNull(session.Token);
        Assert.IsFalse(state.User.Identity?.IsAuthenticated ?? false);
        Assert.AreEqual(2, notifications);
    }

    [TestMethod]
    public void ClearIfCurrent_DoesNotClearReplacementToken()
    {
        Mock<IAccessTokenPrincipalFactory> factory = new();
        factory.Setup(service => service.CreatePrincipal(It.IsAny<string>())).Returns(new ClaimsPrincipal(new ClaimsIdentity("Bearer")));
        AuthenticationSession session = new(factory.Object);
        session.SetToken("replacement", DateTimeOffset.UtcNow.AddMinutes(5));

        bool changed = session.ClearIfCurrent("rejected");

        Assert.IsFalse(changed);
        Assert.AreEqual("replacement", session.Token?.AccessToken);
    }

    [TestMethod]
    public void ClearIfCurrent_ClearsMatchingToken()
    {
        Mock<IAccessTokenPrincipalFactory> factory = new();
        factory.Setup(service => service.CreatePrincipal(It.IsAny<string>())).Returns(new ClaimsPrincipal(new ClaimsIdentity("Bearer")));
        AuthenticationSession session = new(factory.Object);
        session.SetToken("rejected", DateTimeOffset.UtcNow.AddMinutes(5));

        bool changed = session.ClearIfCurrent("rejected");

        Assert.IsTrue(changed);
        Assert.IsNull(session.Token);
    }

    [TestMethod]
    public void SetToken_InvalidPrincipalPayloadIsWrappedWithoutMutatingSession()
    {
        Mock<IAccessTokenPrincipalFactory> factory = new();
        factory.Setup(service => service.CreatePrincipal("bad")).Throws(new InvalidOperationException("bad token"));
        AuthenticationSession session = new(factory.Object);

        ApiProtocolException exception = Assert.ThrowsExactly<ApiProtocolException>(() =>
            session.SetToken("bad", DateTimeOffset.UtcNow.AddMinutes(5)));

        Assert.IsInstanceOfType<InvalidOperationException>(exception.InnerException);
        Assert.IsNull(session.Token);
    }
}
