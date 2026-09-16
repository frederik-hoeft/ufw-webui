using Microsoft.AspNetCore.Components;
using MudBlazor;
using Ufw.Client.Rules.Metadata;

namespace Ufw.Client.Components.Rules.Metadata;

public sealed partial class EditRuleMetadataDialog
{
    private const int MAX_NOTES_LENGTH = 4000;
    private readonly HashSet<Guid> _selectedTagIds = [];
    private MudForm? _form;
    private bool _initialized;
    private string? _notes;
    private RuleTag? _tagToAdd;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public RuleMetadata? Metadata { get; set; }

    [Parameter]
    public IReadOnlyList<RuleTag> AvailableTags { get; set; } = [];

    private IReadOnlyList<RuleTag> SelectedTags => AvailableTags
        .Where(tag => _selectedTagIds.Contains(tag.Id))
        .OrderBy(static tag => tag.Name, StringComparer.CurrentCultureIgnoreCase)
        .ToArray();

    protected override void OnParametersSet()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        _notes = Metadata?.Notes;
        _selectedTagIds.Clear();
        foreach (RuleTag tag in Metadata?.Tags ?? [])
        {
            _selectedTagIds.Add(tag.Id);
        }
    }

    private Task<IEnumerable<RuleTag>> SearchTagsAsync(string? value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IEnumerable<RuleTag> candidates = AvailableTags.Where(tag => !_selectedTagIds.Contains(tag.Id));
        if (!string.IsNullOrWhiteSpace(value))
        {
            string term = value.Trim();
            candidates = candidates.Where(tag => tag.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase));
        }
        return Task.FromResult(candidates);
    }

    private void AddTag(RuleTag? tag)
    {
        if (tag is not null)
        {
            _selectedTagIds.Add(tag.Id);
        }
        _tagToAdd = null;
    }

    private void RemoveTag(Guid tagId) => _selectedTagIds.Remove(tagId);

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

        string? notes = string.IsNullOrWhiteSpace(_notes) ? null : _notes.Trim();
        Guid[] tagIds = [.. _selectedTagIds.Order()];
        MudDialog.Close(DialogResult.Ok(new RuleMetadataEditorResult(notes, tagIds)));
    }

    private static string FormatTag(RuleTag? tag) => tag?.Name ?? string.Empty;
}
