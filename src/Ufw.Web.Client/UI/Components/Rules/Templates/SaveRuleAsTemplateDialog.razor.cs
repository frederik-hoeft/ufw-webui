using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Ufw.Web.Client.UI.Components.Rules.Templates;

public sealed partial class SaveRuleAsTemplateDialog
{
    private MudForm? _form;
    private string _name = string.Empty;
    private string? _description;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public string? CanonicalCommand { get; set; }

    private Func<string?, string?> ValidateName => value => string.IsNullOrWhiteSpace(value) ? TemplatesText["NameRequired"].Value : null;

    private void Cancel() => MudDialog.Cancel();

    private async Task SaveAsync()
    {
        if (_form is null)
        {
            return;
        }

        await _form.ValidateAsync();
        if (!_form.IsValid)
        {
            return;
        }

        MudDialog.Close(DialogResult.Ok(new SaveRuleAsTemplateDialogResult(
            _name.Trim(),
            string.IsNullOrWhiteSpace(_description) ? null : _description.Trim())));
    }
}

public sealed record SaveRuleAsTemplateDialogResult(string Name, string? Description);
