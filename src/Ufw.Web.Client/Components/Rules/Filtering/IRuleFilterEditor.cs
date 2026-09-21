using System.Diagnostics.CodeAnalysis;
using Ufw.Web.Client.Features.Rules.Filtering;

namespace Ufw.Web.Client.Components.Rules.Filtering;

internal interface IRuleFilterEditor
{
    bool TryBuildFilter([NotNullWhen(true)] out RuleFilter? filter);
}
