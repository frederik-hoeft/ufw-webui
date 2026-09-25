using Microsoft.AspNetCore.Components;
using MudBlazor;
using Ufw.Web.Client.Features.Rules.Metadata;
using Ufw.Web.Client.Services.Errors;

namespace Ufw.Web.Client.UI.Components.Rules.Metadata;

public sealed partial class RuleMetadataEditor
{
    internal const int MAX_NOTES_LENGTH = 4000;
    private const int MAX_TAG_NAME_LENGTH = 64;
    private MudForm? _form;
    private MudAutocomplete<TagOption>? _tagAutocomplete;
    private TagOption? _tagToAdd;
    private ClientError? _error;
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
            await TagCatalog.RefreshAsync();
        }
        catch (Exception exception) when (ClientErrors.TryDescribe(exception, out _))
        {
            _error = ClientErrors.Describe(exception);
        }
    }

    public async Task<bool> ValidateAsync()
    {
        if (_form is null)
        {
            return false;
        }

        await _form.ValidateAsync();
        return _form.IsValid;
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
        if (term.Length is > 0 and <= MAX_TAG_NAME_LENGTH && !exactMatch)
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
            IReadOnlyList<RuleTag> tags = await TagCatalog.CreateAsync(name.Trim(), TagColors.Generate());
            return tags.Single(tag => string.Equals(tag.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
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
        await ValueChanged.InvokeAsync(value);
    }

    private static string FormatOption(TagOption? option) => option?.Tag?.Name ?? option?.CreateName ?? string.Empty;

    internal sealed record TagOption(RuleTag? Tag, string? CreateName);
}
