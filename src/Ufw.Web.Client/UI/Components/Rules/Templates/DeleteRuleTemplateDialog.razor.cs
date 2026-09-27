using Microsoft.AspNetCore.Components;
using MudBlazor;
using Ufw.Web.Client.Features.Rules.Templates;

namespace Ufw.Web.Client.UI.Components.Rules.Templates;

public sealed partial class DeleteRuleTemplateDialog
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter, EditorRequired]
    public RuleTemplate Template { get; set; } = null!;

    private void Cancel() => MudDialog.Cancel();

    private void Confirm() => MudDialog.Close(DialogResult.Ok(true));
}
