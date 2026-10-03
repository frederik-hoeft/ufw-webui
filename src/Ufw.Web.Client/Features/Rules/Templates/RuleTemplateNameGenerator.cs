using System.Collections.Immutable;
using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.Rendering;
using Ufw.Shared.Management.Rules;

namespace Ufw.Web.Client.Features.Rules.Templates;

internal sealed class RuleTemplateNameGenerator(IUfwRuleCommandRenderer renderer) : IRuleTemplateNameGenerator
{
    public string Generate(FirewallRuleSpecification rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ImmutableArray<string> arguments = renderer.Render(rule).Arguments;
        int count = arguments.Length;
        if (count >= 2 && arguments[^2] == "comment")
        {
            count -= 2;
        }

        string summary = string.Join(' ', arguments.Take(count));
        if (summary.Length <= RuleTemplateLimits.MAX_NAME_LENGTH)
        {
            return summary;
        }

        // Keep a legible prefix without attempting to make display names unique.
        const string ELLIPSIS = "...";
        int maximumPrefixLength = RuleTemplateLimits.MAX_NAME_LENGTH - ELLIPSIS.Length;
        int boundary = summary.LastIndexOf(' ', maximumPrefixLength);
        return summary[..(boundary > maximumPrefixLength / 2 ? boundary : maximumPrefixLength)].TrimEnd() + ELLIPSIS;
    }
}
