using System.Diagnostics.CodeAnalysis;
using Ufw.Client.Rules.Filtering;

namespace Ufw.Client.Components.Rules.Filtering;

internal interface IRuleFilterEditor
{
    bool TryBuildFilter([NotNullWhen(true)] out RuleFilter? filter);
}
