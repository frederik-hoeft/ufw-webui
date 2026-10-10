using Microsoft.AspNetCore.Components;
using MudBlazor;
using Ufw.Web.Client.Features.Rules.Metadata;
using Ufw.Web.Client.Services.Errors;

namespace Ufw.Web.Client.UI.Components.Rules.Metadata;

public sealed partial class RuleMetadataEditor
{
    private MudForm? _form;
    private MudAutocomplete<TagOption>? _tagAutocomplete;
    private RuleMetadataEditorResult? _loadedValue;
    private GroupOption? _groupSelection;
    private TagOption? _tagToAdd;
    private ClientError? _error;
    private bool _creatingGroup;
    private bool _creatingTag;

    [Parameter, EditorRequired]
    public RuleMetadataEditorResult Value { get; set; } = RuleMetadataEditorResult.Empty;

    [Parameter]
    public EventCallback<RuleMetadataEditorResult> ValueChanged { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    private IReadOnlyList<RuleTag> SelectedTags => Authoring.SelectTags(Value.TagIds);

    protected async override Task OnInitializedAsync()
    {
        try
        {
            await Authoring.RefreshAsync();
            SynchronizeGroupSelection(force: true);
        }
        catch (Exception exception) when (ClientErrors.CanDescribe(exception))
        {
            _error = ClientErrors.Describe(exception);
        }
    }

    protected override void OnParametersSet() => SynchronizeGroupSelection(force: false);

    public async Task<bool> ValidateAsync()
    {
        if (_form is null)
        {
            return false;
        }

        await _form.ValidateAsync();
        return _form.IsValid;
    }

    private Task<IEnumerable<GroupOption>> SearchGroupsAsync(string? value, CancellationToken cancellationToken)
    {
        MetadataCatalogSearchResult<RuleGroup> result = Authoring.SearchGroups(value, cancellationToken);
        List<GroupOption> options = [.. result.Matches.Select(static group => new GroupOption(group, null))];
        if (result.CreateName is { } name)
        {
            options.Add(new GroupOption(null, name));
        }
        return Task.FromResult<IEnumerable<GroupOption>>(options);
    }

    private async Task GroupChangedAsync(GroupOption? option)
    {
        if (Disabled)
        {
            return;
        }
        if (option is null)
        {
            _groupSelection = null;
            await SetValueAsync(Value with { GroupId = null });
            return;
        }

        RuleGroup? group = option.Group;
        if (group is null && option.CreateName is { } createName)
        {
            group = await CreateGroupAsync(createName);
        }
        if (group is null)
        {
            SynchronizeGroupSelection(force: true);
            return;
        }

        _groupSelection = new GroupOption(group, null);
        await SetValueAsync(Value with { GroupId = group.Id });
    }

    private async Task<RuleGroup?> CreateGroupAsync(string name)
    {
        _creatingGroup = true;
        _error = null;
        try
        {
            return await Authoring.CreateGroupAsync(name);
        }
        catch (Exception exception) when (ClientErrors.CanDescribe(exception))
        {
            _error = ClientErrors.Describe(exception);
            return null;
        }
        finally
        {
            _creatingGroup = false;
        }
    }

    private Task<IEnumerable<TagOption>> SearchTagsAsync(string? value, CancellationToken cancellationToken)
    {
        MetadataCatalogSearchResult<RuleTag> result = Authoring.SearchTags(value, Value.TagIds, cancellationToken);
        List<TagOption> options = [.. result.Matches.Select(static tag => new TagOption(tag, null))];
        if (result.CreateName is { } name)
        {
            options.Add(new TagOption(null, name));
        }
        return Task.FromResult<IEnumerable<TagOption>>(options);
    }

    private async Task AddTagAsync(TagOption? option)
    {
        _tagToAdd = null;
        if (option is null || Disabled)
        {
            return;
        }

        RuleTag? tag = option.Tag;
        if (tag is null && option.CreateName is { } createName)
        {
            tag = await CreateTagAsync(createName);
        }
        if (tag is null || Value.TagIds.Contains(tag.Id))
        {
            return;
        }

        Guid[] ids = [.. Value.TagIds.Append(tag.Id).Distinct().Order()];
        await SetValueAsync(Value with { TagIds = ids });
        if (_tagAutocomplete is not null)
        {
            await _tagAutocomplete.ClearAsync();
        }
    }

    private async Task<RuleTag?> CreateTagAsync(string name)
    {
        _creatingTag = true;
        _error = null;
        try
        {
            return await Authoring.CreateTagAsync(name);
        }
        catch (Exception exception) when (ClientErrors.CanDescribe(exception))
        {
            _error = ClientErrors.Describe(exception);
            return null;
        }
        finally
        {
            _creatingTag = false;
        }
    }

    private Task RemoveTagAsync(Guid tagId) => SetValueAsync(Value with { TagIds = Value.TagIds.Where(id => id != tagId).ToArray() });

    private Task NotesChangedAsync(string? notes) => SetValueAsync(Value with { Notes = notes });

    private async Task SetValueAsync(RuleMetadataEditorResult value)
    {
        Value = value;
        _loadedValue = value;
        await ValueChanged.InvokeAsync(value);
    }

    private void SynchronizeGroupSelection(bool force)
    {
        if (!force && ReferenceEquals(_loadedValue, Value))
        {
            return;
        }

        _loadedValue = Value;
        RuleGroup? group = Value.GroupId is { } groupId ? Authoring.FindGroup(groupId) : null;
        _groupSelection = group is null ? null : new GroupOption(group, null);
    }

    private static string FormatGroupOption(GroupOption? option) => option?.Group?.Name ?? option?.CreateName ?? string.Empty;

    private static string FormatTagOption(TagOption? option) => option?.Tag?.Name ?? option?.CreateName ?? string.Empty;

    internal sealed record GroupOption(RuleGroup? Group, string? CreateName);

    internal sealed record TagOption(RuleTag? Tag, string? CreateName);
}
