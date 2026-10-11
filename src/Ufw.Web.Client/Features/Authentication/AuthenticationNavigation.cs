using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Ufw.Shared.Web;

namespace Ufw.Web.Client.Features.Authentication;

internal sealed class AuthenticationNavigation : IAuthenticationNavigation, IDisposable
{
    private readonly NavigationManager _navigation;
    private readonly AuthenticationSession _session;
    private int _redirectPending;

    public AuthenticationNavigation(NavigationManager navigation, AuthenticationSession session)
    {
        _navigation = navigation;
        _session = session;
        session.AuthenticationStateChanged += OnAuthenticationStateChanged;
        navigation.LocationChanged += OnLocationChanged;
    }

    public void RedirectToLogin()
    {
        if (_session.Token is not null)
        {
            return;
        }

        string relative = _navigation.ToBaseRelativePath(_navigation.Uri);
        if (IsLoginRoute(relative) || Interlocked.CompareExchange(ref _redirectPending, 1, 0) != 0)
        {
            return;
        }

        // Preserve the complete local route, including its query and fragment, as one encoded return URL.
        string returnUrl = string.IsNullOrEmpty(relative) ? "/" : "/" + relative;
        string loginUri = SimpleUriBuilder.Create("login").AppendQuery("returnUrl", returnUrl).Build();
        try
        {
            _navigation.NavigateTo(loginUri, replace: true);
        }
        catch
        {
            Interlocked.Exchange(ref _redirectPending, 0);
            throw;
        }
    }

    private void OnAuthenticationStateChanged(Task<Microsoft.AspNetCore.Components.Authorization.AuthenticationState> _)
    {
        if (_session.Token is not null)
        {
            Interlocked.Exchange(ref _redirectPending, 0);
        }
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        string relative = _navigation.ToBaseRelativePath(args.Location);
        if (IsLoginRoute(relative))
        {
            Interlocked.Exchange(ref _redirectPending, 0);
        }
    }

    private static bool IsLoginRoute(string relativePath) =>
        string.Equals(relativePath.Split(['?', '#'], 2)[0].Trim('/'), "login", StringComparison.OrdinalIgnoreCase);

    public void Dispose()
    {
        _session.AuthenticationStateChanged -= OnAuthenticationStateChanged;
        _navigation.LocationChanged -= OnLocationChanged;
    }
}
