using System.Collections.Immutable;
using System.Globalization;
using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.Rendering;

namespace Ufw.Systemd.Interop.Commands;

internal sealed class UfwInsertRuleCommand(
    int displayNumber,
    FirewallRuleSpecification specification,
    IUfwRuleCommandRenderer renderer) : IUfwCommand
{
    public ImmutableArray<string> BuildArguments()
    {
        if (displayNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(displayNumber), displayNumber, "UFW rule numbers are 1-based.");
        }

        ImmutableArray<string> ruleArguments = renderer.Render(specification).Arguments;
        string number = displayNumber.ToString(CultureInfo.InvariantCulture);
        if (ruleArguments.Length > 0 && string.Equals(ruleArguments[0], "route", StringComparison.Ordinal))
        {
            return ["route", "insert", number, .. ruleArguments[1..]];
        }

        return ["insert", number, .. ruleArguments];
    }

    public void SetOutput(string output)
    {
    }
}
