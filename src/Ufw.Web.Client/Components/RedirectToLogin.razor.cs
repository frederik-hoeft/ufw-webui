using Ufw.Shared.Web;

namespace Ufw.Web.Client.Components;

public sealed partial class RedirectToLogin
{
    protected override void OnInitialized()
    {
        string relativePath = Navigation.ToBaseRelativePath(Navigation.Uri);
        string returnUrl = string.IsNullOrWhiteSpace(relativePath) ? "/" : SimpleUriBuilder.Create("/").AppendPath(relativePath).Build();
        string loginUri = SimpleUriBuilder.Create("login")
            .AppendQuery("returnUrl", returnUrl)
            .Build();
        Navigation.NavigateTo(loginUri, replace: true);
    }
}
