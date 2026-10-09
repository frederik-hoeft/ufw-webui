using Moq;
using System.Security.Claims;
using Ufw.Web.Client.Features.Authentication;
using Ufw.Web.Client.Tests.Support;

namespace Ufw.Web.Client.Tests.Features.Authentication;

[TestClass]
public sealed class AuthenticationNavigationTests
{
    [TestMethod]
    public void RedirectToLogin_PreservesDeepLinkQueryAndIsIdempotent()
    {
        TestNavigationManager navigation = new("https://localhost/rules/create?family=ipv6&template=abc");
        using AuthenticationNavigation redirect = Create(navigation, out AuthenticationSession session);

        redirect.RedirectToLogin();
        string? first = navigation.LastUri;
        redirect.RedirectToLogin();

        Assert.IsNotNull(first);
        Uri uri = new(first);
        Assert.AreEqual("/login", uri.AbsolutePath);
        Assert.AreEqual("/rules/create?family=ipv6&template=abc", Uri.UnescapeDataString(uri.Query["?returnUrl=".Length..]));
        Assert.AreEqual(first, navigation.LastUri);
        Assert.AreEqual(1, navigation.NavigateCount);
        Assert.IsNull(session.Token);
    }

    [TestMethod]
    public void RedirectToLogin_DoesNotRedirectFromLoginOrAuthenticatedSession()
    {
        TestNavigationManager login = new("https://localhost/login?returnUrl=%2Frules");
        using AuthenticationNavigation loginRedirect = Create(login, out _);
        loginRedirect.RedirectToLogin();
        Assert.IsNull(login.LastUri);

        TestNavigationManager rules = new("https://localhost/rules");
        using AuthenticationNavigation authenticatedRedirect = Create(rules, out AuthenticationSession session);
        session.SetToken("valid", DateTimeOffset.UtcNow.AddMinutes(2));
        authenticatedRedirect.RedirectToLogin();
        Assert.IsNull(rules.LastUri);
    }

    [TestMethod]
    public void RedirectToLogin_ResetsAfterNewLoginForSubsequentExpiry()
    {
        TestNavigationManager navigation = new("https://localhost/rules");
        using AuthenticationNavigation redirect = Create(navigation, out AuthenticationSession session);
        redirect.RedirectToLogin();
        session.SetToken("new", DateTimeOffset.UtcNow.AddMinutes(2));
        session.Clear();
        redirect.RedirectToLogin();

        Assert.AreEqual(2, navigation.NavigateCount);
    }

    [TestMethod]
    public void RedirectToLogin_ConcurrentRequestsNavigateOnce()
    {
        TestNavigationManager navigation = new("https://localhost/rules");
        using AuthenticationNavigation redirect = Create(navigation, out _);

        Parallel.For(0, 16, _ => redirect.RedirectToLogin());

        Assert.AreEqual(1, navigation.NavigateCount);
    }

    [TestMethod]
    public void RedirectToLogin_DifferentTabSessionsRemainIndependent()
    {
        TestNavigationManager firstNavigation = new("https://localhost/rules");
        TestNavigationManager secondNavigation = new("https://localhost/templates");
        using AuthenticationNavigation first = Create(firstNavigation, out AuthenticationSession firstSession);
        using AuthenticationNavigation second = Create(secondNavigation, out AuthenticationSession secondSession);
        firstSession.SetToken("first", DateTimeOffset.UtcNow.AddMinutes(2));
        secondSession.SetToken("second", DateTimeOffset.UtcNow.AddMinutes(2));

        firstSession.Clear();
        first.RedirectToLogin();
        second.RedirectToLogin();

        Assert.AreEqual(1, firstNavigation.NavigateCount);
        Assert.AreEqual(0, secondNavigation.NavigateCount);

        secondSession.Clear();
        second.RedirectToLogin();
        Assert.AreEqual(1, secondNavigation.NavigateCount);
    }

    private static AuthenticationNavigation Create(TestNavigationManager navigation, out AuthenticationSession session)
    {
        Mock<IAccessTokenPrincipalFactory> principalFactory = new();
        principalFactory.Setup(factory => factory.CreatePrincipal(It.IsAny<string>()))
            .Returns(new ClaimsPrincipal(new ClaimsIdentity("Bearer")));
        session = new AuthenticationSession(principalFactory.Object);
        return new AuthenticationNavigation(navigation, session);
    }
}
