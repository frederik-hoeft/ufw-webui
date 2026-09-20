using Microsoft.AspNetCore.Components;
using MudBlazor;
using Ufw.Client.Errors;
using Ufw.Client.Rules.Metadata;

namespace Ufw.Client.Components.Rules.Metadata;

public sealed partial class ReconcileRuleMetadataDialog
{
    private static readonly DialogOptions s_cleanupDialogOptions = new()
    {
        BackdropClick = false,
        CloseButton = true,
        CloseOnEscapeKey = true,
        FullWidth = true,
        MaxWidth = MaxWidth.ExtraSmall,
    };

    private readonly HashSet<Guid> _selected = [];
    private RuleMetadataReconciliationSnapshot? _snapshot;
    private ClientError? _error;
    private bool _loading;
    private bool _cleaning;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    private bool IsBusy => _loading || _cleaning;

    protected override Task OnInitializedAsync() => RefreshAsync();

    private async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        _loading = true;
        _error = null;
        try
        {
            _snapshot = await Reconciliation.RefreshAsync();
            ReconcileSelection();
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

    private async Task CleanupAsync()
    {
        if (IsBusy || _selected.Count == 0)
        {
            return;
        }

        DialogParameters<CleanupRuleMetadataDialog> parameters = [];
        parameters.Add(component => component.Count, _selected.Count);
        IDialogReference dialog = await DialogService.ShowAsync<CleanupRuleMetadataDialog>(RulesText["CleanupOrphanedMetadata"], parameters, s_cleanupDialogOptions);
        if (await dialog.GetReturnValueAsync<bool?>() != true)
        {
            return;
        }

        _cleaning = true;
        _error = null;
        try
        {
            _snapshot = await Reconciliation.CleanupAsync(_selected);
            int removedCount = _snapshot.RemovedCount;
            ReconcileSelection();
            Snackbar.Add(RulesText["MetadataCleanupCompleted", removedCount], Severity.Success);
        }
        catch (Exception exception) when (ClientErrors.TryDescribe(exception, out _))
        {
            _error = ClientErrors.Describe(exception);
            Snackbar.Add(_error.Message, Severity.Error);
        }
        finally
        {
            _cleaning = false;
        }
    }

    private void SelectAll()
    {
        if (IsBusy || _snapshot is null)
        {
            return;
        }
        _selected.Clear();
        foreach (OrphanedRuleMetadata orphan in _snapshot.Orphans)
        {
            _selected.Add(orphan.Metadata.Id);
        }
    }

    private void ClearSelection() => _selected.Clear();

    private void SelectionChanged(Guid metadataId, bool selected)
    {
        if (selected)
        {
            _selected.Add(metadataId);
        }
        else
        {
            _selected.Remove(metadataId);
        }
    }

    private void ReconcileSelection()
    {
        if (_snapshot is null)
        {
            _selected.Clear();
            return;
        }

        HashSet<Guid> available = _snapshot.Orphans.Select(static orphan => orphan.Metadata.Id).ToHashSet();
        _selected.RemoveWhere(id => !available.Contains(id));
    }

    private string DescribeOrphanCount(int count) => count == 1
        ? RulesText["OrphanedMetadataCountOne"]
        : RulesText["OrphanedMetadataCountMany", count];

    private static string DescribeRuleId(string ruleId) => ruleId.Length <= 20 ? ruleId : $"{ruleId[..20]}\u2026";

    private void Close() => MudDialog.Close();
}
