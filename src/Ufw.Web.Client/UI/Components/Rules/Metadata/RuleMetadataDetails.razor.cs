using Microsoft.AspNetCore.Components;
using MudBlazor;
using Ufw.Web.Client.Features.Rules.Metadata;
using Ufw.Web.Client.Services.Clipboard;

namespace Ufw.Web.Client.UI.Components.Rules.Metadata;

public sealed partial class RuleMetadataDetails
{
    [Inject]
    private IClipboardService Clipboard { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    [Parameter]
    public RuleMetadata? Metadata { get; set; }

    [Parameter]
    public string? CanonicalCommand { get; set; }

    [Parameter]
    public bool EditDisabled { get; set; }

    [Parameter]
    public EventCallback EditRequested { get; set; }

    [Parameter]
    public RenderFragment? Actions { get; set; }

    private async Task CopyCanonicalCommandAsync()
    {
        if (string.IsNullOrWhiteSpace(CanonicalCommand))
        {
            return;
        }

        try
        {
            await Clipboard.WriteTextAsync(CanonicalCommand);
            Snackbar.Add(RulesText["CanonicalCommandCopied"], Severity.Success);
        }
        catch (Exception)
        {
            Snackbar.Add(RulesText["CanonicalCommandCopyFailed"], Severity.Error);
        }
    }
}
