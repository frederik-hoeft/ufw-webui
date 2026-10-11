namespace Ufw.Web.Client.UI.Components;

public sealed partial class RedirectToLogin
{
    protected override void OnInitialized() => AuthenticationNavigation.RedirectToLogin();
}
