using Microsoft.AspNetCore.Components;
using MudBlazor;
using Ufw.Shared.Management.Rules;
using Ufw.Web.Client.Features.Rules.Metadata;
using Ufw.Web.Client.Services.Errors;

namespace Ufw.Web.Client.UI.Components.Rules.Metadata;

public sealed partial class ManageRuleTagsDialog
{
    private static readonly DialogOptions s_deleteDialogOptions = ClientDialogOptions.Compact;

    private IReadOnlyList<RuleTag> _tags = [];
    private ClientError? _error;
    private RuleTag? _editingTag;
    private string _name = string.Empty;
    private string _color = string.Empty;
    private bool _loading;
    private bool _saving;

    private bool Editing { get; set; }

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    private bool IsBusy => _loading || _saving;
    private bool IsCreating => Editing && _editingTag is null;
    private RuleTag PreviewTag => new(Guid.Empty, string.IsNullOrWhiteSpace(_name) ? "\u2026" : _name.Trim(), _color);

    protected override Task OnInitializedAsync() => RefreshAsync();

    private async Task RefreshAsync()
    {
        _loading = true;
        _error = null;
        try
        {
            _tags = await Authoring.RefreshTagsAsync();
        }
        catch (Exception exception) when (ClientErrors.CanDescribe(exception))
        {
            _error = ClientErrors.Describe(exception);
        }
        finally
        {
            _loading = false;
        }
    }

    private void BeginCreate()
    {
        if (IsBusy)
        {
            return;
        }
        _editingTag = null;
        _name = string.Empty;
        _color = Authoring.GenerateTagColor();
        Editing = true;
    }

    private void BeginEdit(RuleTag tag)
    {
        if (IsBusy)
        {
            return;
        }
        _editingTag = tag;
        _name = tag.Name;
        _color = tag.Color;
        Editing = true;
    }

    private void CancelEdit()
    {
        if (!_saving)
        {
            Editing = false;
            _editingTag = null;
        }
    }

    private void ReshuffleColor() => _color = Authoring.GenerateTagColor();

    private async Task SaveTagAsync()
    {
        if (_saving)
        {
            return;
        }

        string name = _name.Trim();
        if (name.Length is 0 or > RuleTagLimits.MAX_NAME_LENGTH)
        {
            return;
        }
        if (!RuleTagColor.TryNormalize(_color, out string? color))
        {
            throw new InvalidOperationException("Generated tag color is invalid.");
        }

        _saving = true;
        _error = null;
        try
        {
            if (_editingTag is null)
            {
                _ = await Authoring.CreateTagAsync(name, color);
                _tags = Authoring.Tags;
            }
            else
            {
                _tags = await Authoring.UpdateTagAsync(_editingTag.Id, name, color);
            }
            Editing = false;
            _editingTag = null;
            Snackbar.Add(RulesText["TagSaved"], Severity.Success);
        }
        catch (Exception exception) when (ClientErrors.CanDescribe(exception))
        {
            _error = ClientErrors.Describe(exception);
            Snackbar.Add(_error.Message, Severity.Error);
        }
        finally
        {
            _saving = false;
        }
    }

    private async Task DeleteTagAsync(RuleTag tag)
    {
        if (IsBusy)
        {
            return;
        }

        DialogParameters<DeleteRuleTagDialog> parameters = [];
        parameters.Add(component => component.Tag, tag);
        IDialogReference dialog = await DialogService.ShowAsync<DeleteRuleTagDialog>(RulesText["DeleteTag"], parameters, s_deleteDialogOptions);
        bool? confirmed = await dialog.GetReturnValueAsync<bool?>();
        if (confirmed != true)
        {
            return;
        }

        _saving = true;
        _error = null;
        try
        {
            _tags = await Authoring.DeleteTagAsync(tag.Id);
            Snackbar.Add(RulesText["TagDeleted"], Severity.Success);
        }
        catch (Exception exception) when (ClientErrors.CanDescribe(exception))
        {
            _error = ClientErrors.Describe(exception);
            Snackbar.Add(_error.Message, Severity.Error);
        }
        finally
        {
            _saving = false;
        }
    }

    private void Close() => MudDialog.Close();
}
