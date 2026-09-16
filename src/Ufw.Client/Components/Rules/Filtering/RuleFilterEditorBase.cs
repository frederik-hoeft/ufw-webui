using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;
using Ufw.Client.Rules.Filtering;
using Ufw.Shared.Firewall;

namespace Ufw.Client.Components.Rules.Filtering;

public abstract class RuleFilterEditorBase : ComponentBase, IRuleFilterEditor
{
    [Parameter]
    public RuleFilter? Filter { get; set; }

    [Parameter]
    public FirewallAddressFamily AddressFamily { get; set; }

    public abstract bool TryBuildFilter([NotNullWhen(true)] out RuleFilter? filter);
}
