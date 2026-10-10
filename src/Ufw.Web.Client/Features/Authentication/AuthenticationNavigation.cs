using Microsoft.AspNetCore.Components;
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
    }

    public void RedirectToLogin()
    {
        if (_session.Token is not null)
        {
            return;
        }

        string relative = _navigation.ToBaseRelativePath(_navigation.Uri);
        string path = relative.Split(['?', '#'], 2)[0].Trim('/');
        if (string.Equals(path, "login", StringComparison.OrdinalIgnoreCase)
            || Interlocked.CompareExchange(ref _redirectPending, 1, 0) != 0)
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

    public void Dispose() => _session.AuthenticationStateChanged -= OnAuthenticationStateChanged;
}
