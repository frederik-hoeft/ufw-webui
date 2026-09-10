using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Ufw.Client.Components.Rules;

public sealed partial class MoveRuleDialog
{
    private int _targetPosition;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public int CurrentPosition { get; set; }

    [Parameter]
    public int RuleCount { get; set; }

    private bool CanConfirm => _targetPosition >= 1
        && _targetPosition <= RuleCount
        && _targetPosition != CurrentPosition;

    protected override void OnParametersSet()
    {
        if (_targetPosition == 0)
        {
            _targetPosition = CurrentPosition;
        }
    }

    private void Cancel() => MudDialog.Cancel();

    private void Confirm()
    {
        if (CanConfirm)
        {
            MudDialog.Close(DialogResult.Ok(_targetPosition));
        }
    }
}
