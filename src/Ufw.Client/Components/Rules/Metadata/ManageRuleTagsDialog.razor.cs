using Microsoft.AspNetCore.Components;
using MudBlazor;
using Ufw.Client.Errors;
using Ufw.Client.Rules.Metadata;

namespace Ufw.Client.Components.Rules.Metadata;

public sealed partial class ManageRuleTagsDialog
{
    private const int MAX_TAG_NAME_LENGTH = 64;
    private static readonly DialogOptions s_deleteDialogOptions = new()
    {
        BackdropClick = false,
        CloseButton = true,
        CloseOnEscapeKey = true,
        FullWidth = true,
        MaxWidth = MaxWidth.ExtraSmall,
    };

    private IReadOnlyList<RuleTag> _tags = [];
    private ClientError? _error;
    private RuleTag? _editingTag;
    private string _name = string.Empty;
    private string _color = string.Empty;
    private bool _loading;
    private bool _saving;
    private bool _editing;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    private bool IsBusy => _loading || _saving;

    protected override Task OnInitializedAsync() => RefreshAsync();

    private async Task RefreshAsync()
    {
        _loading = true;
        _error = null;
        try
        {
            _tags = await TagCatalog.RefreshAsync();
        }
        catch (Exception exception) when (ClientErrors.TryDescribe(exception, out _))
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
        _color = TagColors.Generate();
        _editing = true;
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
        _editing = true;
    }

    private void CancelEdit()
    {
        if (!_saving)
        {
            _editing = false;
            _editingTag = null;
        }
    }

    private void ReshuffleColor() => _color = TagColors.Generate();

    private async Task SaveTagAsync()
    {
        if (_saving)
        {
            return;
        }

        string name = _name.Trim();
        if (name.Length is 0 or > MAX_TAG_NAME_LENGTH)
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
            _tags = _editingTag is null
                ? await TagCatalog.CreateAsync(name, color)
                : await TagCatalog.UpdateAsync(_editingTag.Id, name, color);
            _editing = false;
            _editingTag = null;
            Snackbar.Add(RulesText["TagSaved"], Severity.Success);
        }
        catch (Exception exception) when (ClientErrors.TryDescribe(exception, out _))
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
        if (await dialog.GetReturnValueAsync<bool?>() != true)
        {
            return;
        }

        _saving = true;
        _error = null;
        try
        {
            _tags = await TagCatalog.DeleteAsync(tag.Id);
            Snackbar.Add(RulesText["TagDeleted"], Severity.Success);
        }
        catch (Exception exception) when (ClientErrors.TryDescribe(exception, out _))
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
