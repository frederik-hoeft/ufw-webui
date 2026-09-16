using Microsoft.AspNetCore.Components;
using Ufw.Client.Rules.Metadata;

namespace Ufw.Client.Components.Rules.Metadata;

public sealed partial class RuleMetadataDetails
{
    [Parameter]
    public RuleMetadata? Metadata { get; set; }

    [Parameter]
    public bool EditDisabled { get; set; }

    [Parameter]
    public EventCallback EditRequested { get; set; }
}
