using Microsoft.AspNetCore.Components;
using Ufw.Client.Errors;

namespace Ufw.Client.Components;

public sealed partial class UnexpectedError
{
    [Parameter, EditorRequired]
    public ClientError Error { get; set; } = null!;

    private void Reload() => Navigation.Refresh(forceReload: true);
}
