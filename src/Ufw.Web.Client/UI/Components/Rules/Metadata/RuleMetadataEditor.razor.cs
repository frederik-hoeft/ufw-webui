using Microsoft.AspNetCore.Components;
using MudBlazor;
using Ufw.Web.Client.Features.Rules.Metadata;
using Ufw.Web.Client.Services.Errors;

namespace Ufw.Web.Client.UI.Components.Rules.Metadata;

public sealed partial class RuleMetadataEditor
{
    internal const int MAX_NOTES_LENGTH = 4000;
    private const int MAX_METADATA_NAME_LENGTH = 64;
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

    private IReadOnlyList<RuleTag> SelectedTags => TagCatalog.Current
        .Where(tag => Value.TagIds.Contains(tag.Id))
        .OrderBy(static tag => tag.Name, StringComparer.CurrentCultureIgnoreCase)
        .ToArray();

    protected async override Task OnInitializedAsync()
    {
        try
        {
            await Task.WhenAll(TagCatalog.RefreshAsync(), GroupCatalog.RefreshAsync());
            SynchronizeGroupSelection(force: true);
        }
        catch (Exception exception) when (ClientErrors.TryDescribe(exception, out _))
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
        cancellationToken.ThrowIfCancellationRequested();
        string term = value?.Trim() ?? string.Empty;
        IEnumerable<RuleGroup> available = GroupCatalog.Current;
        if (term.Length > 0)
        {
            available = available.Where(group => group.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase));
        }

        List<GroupOption> options = available.Select(static group => new GroupOption(group, null)).ToList();
        bool exactMatch = GroupCatalog.Current.Any(group => string.Equals(group.Name, term, StringComparison.OrdinalIgnoreCase));
        if (term.Length is > 0 and <= MAX_METADATA_NAME_LENGTH && !exactMatch)
        {
            options.Add(new GroupOption(null, term));
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
            string normalizedName = name.Trim();
            IReadOnlyList<RuleGroup> groups = await GroupCatalog.CreateAsync(normalizedName);
            return groups.Single(group => string.Equals(group.Name, normalizedName, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception exception) when (ClientErrors.TryDescribe(exception, out _))
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
        cancellationToken.ThrowIfCancellationRequested();
        string term = value?.Trim() ?? string.Empty;
        IEnumerable<RuleTag> available = TagCatalog.Current.Where(tag => !Value.TagIds.Contains(tag.Id));
        if (term.Length > 0)
        {
            available = available.Where(tag => tag.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase));
        }

        List<TagOption> options = available.Select(static tag => new TagOption(tag, null)).ToList();
        bool exactMatch = TagCatalog.Current.Any(tag => string.Equals(tag.Name, term, StringComparison.OrdinalIgnoreCase));
        if (term.Length is > 0 and <= MAX_METADATA_NAME_LENGTH && !exactMatch)
        {
            options.Add(new TagOption(null, term));
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
            string normalizedName = name.Trim();
            IReadOnlyList<RuleTag> tags = await TagCatalog.CreateAsync(normalizedName, TagColors.Generate());
            return tags.Single(tag => string.Equals(tag.Name, normalizedName, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception exception) when (ClientErrors.TryDescribe(exception, out _))
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
        _groupSelection = Value.GroupId is { } groupId
            ? GroupCatalog.Current.Where(group => group.Id == groupId).Select(static group => new GroupOption(group, null)).FirstOrDefault()
            : null;
    }

    private static string FormatGroupOption(GroupOption? option) => option?.Group?.Name ?? option?.CreateName ?? string.Empty;

    private static string FormatTagOption(TagOption? option) => option?.Tag?.Name ?? option?.CreateName ?? string.Empty;

    internal sealed record GroupOption(RuleGroup? Group, string? CreateName);

    internal sealed record TagOption(RuleTag? Tag, string? CreateName);
}
