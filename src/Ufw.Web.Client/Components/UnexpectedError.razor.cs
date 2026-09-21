using Microsoft.AspNetCore.Components;
using Ufw.Web.Client.Services.Errors;

namespace Ufw.Web.Client.Components;

public sealed partial class UnexpectedError
{
    [Parameter, EditorRequired]
    public ClientError Error { get; set; } = null!;

    private void Reload() => Navigation.Refresh(forceReload: true);
}
