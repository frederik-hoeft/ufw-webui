using System.Text;
using Ufw.Mock.State;
using Ufw.Shared.Firewall;

namespace Ufw.Mock.Formatting;

/// <summary>Produces the UFW translation file used for optional managed-rule attribution.</summary>
internal static class MockUserRulesFormatter
{
    public static string Format(UfwMockState state, FirewallAddressFamily family)
    {
        StringBuilder builder = new();
        builder.AppendLine("# Ufw.Mock generated user rules");
        builder.AppendLine("*filter");
        foreach (string suffix in new[] { "user-input", "user-output", "user-forward", "user-limit", "user-limit-accept", "user-logging-input", "user-logging-output", "user-logging-forward" })
        {
            builder.Append(':').Append(MockChainNames.Prefix(family)).Append(suffix).AppendLine(" - [0:0]");
        }
        builder.AppendLine("### RULES ###");
        foreach (UfwMockRule rule in state.Rules.Where(rule => rule.Specification.AddressFamily == family))
        {
            builder.AppendLine();
            builder.AppendLine(MockRuleExpansion.FormatTuple(rule));
            foreach (string declaration in MockRuleExpansion.FormatDeclarations(rule))
            {
                builder.AppendLine(declaration);
            }
        }
        builder.AppendLine("### END RULES ###");
        builder.AppendLine("COMMIT");
        return builder.ToString();
    }
}
