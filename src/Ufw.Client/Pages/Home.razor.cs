namespace Ufw.Client.Pages;

public sealed partial class Home
{
    protected override void OnInitialized() => Navigation.NavigateTo("/rules", replace: true);
}
