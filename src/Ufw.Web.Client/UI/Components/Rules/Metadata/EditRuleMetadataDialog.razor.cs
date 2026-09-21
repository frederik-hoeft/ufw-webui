using Microsoft.AspNetCore.Components;
using MudBlazor;
using Ufw.Web.Client.Features.Rules.Metadata;

namespace Ufw.Web.Client.UI.Components.Rules.Metadata;

public sealed partial class EditRuleMetadataDialog
{
    private RuleMetadataEditor? _editor;
    private RuleMetadataEditorResult _value = RuleMetadataEditorResult.Empty;
    private RuleMetadata? _loadedMetadata;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public RuleMetadata? Metadata { get; set; }

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(_loadedMetadata, Metadata))
        {
            return;
        }

        _loadedMetadata = Metadata;
        _value = RuleMetadataEditorResult.FromMetadata(Metadata);
    }

    private void Cancel() => MudDialog.Cancel();

    private async Task SaveAsync()
    {
        if (_editor is null || !await _editor.ValidateAsync())
        {
            return;
        }

        MudDialog.Close(DialogResult.Ok(_value.Normalize()));
    }
}
