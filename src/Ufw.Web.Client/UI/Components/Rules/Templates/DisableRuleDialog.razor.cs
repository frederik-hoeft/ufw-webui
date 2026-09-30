using Microsoft.AspNetCore.Components;
using MudBlazor;
using Ufw.Web.Client.Features.Rules;

namespace Ufw.Web.Client.UI.Components.Rules.Templates;

public sealed partial class DisableRuleDialog
{
    private MudForm? _form;
    private bool _isValid;
    private bool _busy;
    private string? _description;
    private string _privateKey = string.Empty;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter, EditorRequired]
    public RuleRowProjection Row { get; set; } = null!;

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
            MudDialog.Close(DialogResult.Ok(new DisableRuleDialogResult(
                string.IsNullOrWhiteSpace(_description) ? null : _description.Trim(),
                privateKey)));
        }
        finally
        {
            _busy = false;
        }
    }
}

public sealed record DisableRuleDialogResult(string? Description, string PrivateKey);
