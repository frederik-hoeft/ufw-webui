using Microsoft.AspNetCore.Components;
using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Firewall;
using Ufw.Web.Client.Features.Rules.Filtering;

namespace Ufw.Web.Client.UI.Components.Rules.Filtering;

public abstract class RuleFilterEditorBase : ComponentBase, IRuleFilterEditor
{
    [Parameter]
    public RuleFilter? Filter { get; set; }

    [Parameter]
    public FirewallAddressFamily AddressFamily { get; set; }

    public abstract bool TryBuildFilter([NotNullWhen(true)] out RuleFilter? filter);
}
