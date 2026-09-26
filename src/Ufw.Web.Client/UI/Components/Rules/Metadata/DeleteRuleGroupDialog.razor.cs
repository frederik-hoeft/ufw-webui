using Microsoft.AspNetCore.Components;
using MudBlazor;
using Ufw.Web.Client.Features.Rules.Metadata;

namespace Ufw.Web.Client.UI.Components.Rules.Metadata;

public sealed partial class DeleteRuleGroupDialog : IDisposable
{
    private MudForm? _form;
    private bool _isValid;
    private bool _busy;
    private string _privateKey = string.Empty;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter, EditorRequired]
    public RuleGroupManagementProjection Projection { get; set; } = null!;

    private bool RequiresSignedMutation => Projection.StoredMemberCount != 0;

    private bool CanDeleteRules => Projection.MemberResolutionAvailable && Projection.StaleMembershipCount == 0 && Projection.LiveOccurrenceCount > 0;

    public void Dispose() => _privateKey = string.Empty;

    private void Cancel()
    {
        _privateKey = string.Empty;
        MudDialog.Cancel();
    }

    private async Task ConfirmAsync()
    {
        if (_busy || RequiresSignedMutation && !CanDeleteRules)
        {
            return;
        }

        _busy = true;
        try
        {
            if (RequiresSignedMutation)
            {
                if (_form is null)
                {
                    return;
                }

                await _form.ValidateAsync();
                if (!_form.IsValid || string.IsNullOrWhiteSpace(_privateKey))
                {
                    return;
                }
            }

            string? privateKey = RequiresSignedMutation ? _privateKey : null;
            _privateKey = string.Empty;
            MudDialog.Close(DialogResult.Ok(new RuleGroupDeleteDialogResult(privateKey)));
        }
        finally
        {
            _busy = false;
        }
    }
}
