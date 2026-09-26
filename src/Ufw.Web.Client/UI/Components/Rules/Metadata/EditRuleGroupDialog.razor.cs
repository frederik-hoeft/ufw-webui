using Microsoft.AspNetCore.Components;
using MudBlazor;
using Ufw.Web.Client.Features.Rules.Metadata;
using Ufw.Web.Client.Services.Errors;

namespace Ufw.Web.Client.UI.Components.Rules.Metadata;

public sealed partial class EditRuleGroupDialog
{
    private const int MAX_GROUP_NAME_LENGTH = 64;
    private const int MAX_GROUP_COMMENT_LENGTH = 4000;

    private ClientError? _error;
    private string _name = string.Empty;
    private string? _comment;
    private bool _saving;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public RuleGroup? Group { get; set; }

    private bool CanSave => !_saving && _name.Trim().Length is > 0 and <= MAX_GROUP_NAME_LENGTH && (_comment?.Trim().Length ?? 0) <= MAX_GROUP_COMMENT_LENGTH;

    protected override void OnInitialized()
    {
        if (Group is not null)
        {
            _name = Group.Name;
            _comment = Group.Comment;
        }
    }

    private async Task SaveAsync()
    {
        if (!CanSave)
        {
            return;
        }

        _saving = true;
        _error = null;
        try
        {
            string name = _name.Trim();
            string? comment = string.IsNullOrWhiteSpace(_comment) ? null : _comment.Trim();
            if (Group is null)
            {
                await GroupCatalog.CreateAsync(name, comment);
            }
            else
            {
                await GroupCatalog.UpdateAsync(Group.Id, name, comment);
            }
            MudDialog.Close(DialogResult.Ok(true));
        }
        catch (Exception exception) when (ClientErrors.TryDescribe(exception, out _))
        {
            _error = ClientErrors.Describe(exception);
        }
        finally
        {
            _saving = false;
        }
    }

    private void Cancel() => MudDialog.Cancel();
}
