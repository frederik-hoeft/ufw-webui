using Microsoft.AspNetCore.Components;
using MudBlazor;
using Ufw.Shared.Firewall;

namespace Ufw.Client.Components.Rules;

public sealed partial class DeleteRuleDialog
{
    private MudForm? _form;
    private bool _isValid;
    private bool _busy;
    private string _privateKey = string.Empty;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter, EditorRequired]
    public ListedFirewallRule Rule { get; set; } = null!;

    public void Dispose() => _privateKey = string.Empty;

    private void Cancel()
    {
        _privateKey = string.Empty;
        MudDialog.Cancel();
    }

    private async Task ConfirmAsync()
    {
        if (_busy || _form is null)
        {
            return;
        }

        _busy = true;
        try
        {
            await _form.ValidateAsync();
            if (!_form.IsValid || string.IsNullOrWhiteSpace(_privateKey))
            {
                return;
            }

            string privateKey = _privateKey;
            _privateKey = string.Empty;
            MudDialog.Close(DialogResult.Ok(privateKey));
        }
        finally
        {
            _busy = false;
        }
    }
}
