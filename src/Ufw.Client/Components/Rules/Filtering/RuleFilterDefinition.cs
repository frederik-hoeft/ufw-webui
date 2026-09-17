using Microsoft.Extensions.Localization;
using Ufw.Client.Localization;
using Ufw.Client.Rules.Presentation;
using Ufw.Client.Rules.Filtering;

namespace Ufw.Client.Components.Rules.Filtering;

internal sealed record RuleFilterDefinition(
    string Key,
    string DisplayNameResourceKey,
    string CategoryResourceKey,
    Type EditorComponentType,
    Func<RuleFilter, bool> Matches,
    Func<RuleFilter, IFirewallRuleText, IStringLocalizer<RulesStrings>, string> Describe,
    bool Selectable = true,
    IReadOnlyDictionary<string, object>? EditorParameters = null)
{
    public IReadOnlyDictionary<string, object> Parameters { get; } = EditorParameters ?? new Dictionary<string, object>();
}
