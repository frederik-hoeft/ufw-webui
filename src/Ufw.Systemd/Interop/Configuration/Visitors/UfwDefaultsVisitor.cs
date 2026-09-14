using Ufw.Shared.Firewall;
using Ufw.Systemd.Interop.Configuration.SyntaxNodes;

namespace Ufw.Systemd.Interop.Configuration.Visitors;

internal sealed class UfwDefaultsVisitor : IUfwDefaultsVisitor
{
    private const string IPV6 = "IPV6";
    private const string DEFAULT_INPUT_POLICY = "DEFAULT_INPUT_POLICY";
    private const string DEFAULT_OUTPUT_POLICY = "DEFAULT_OUTPUT_POLICY";
    private const string DEFAULT_FORWARD_POLICY = "DEFAULT_FORWARD_POLICY";

    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
    private bool _hasInvalidRequiredAssignment;

    public void Visit(UfwDefaultsAssignmentSyntaxNode syntaxNode)
    {
        UfwDefaultsAssignment assignment = syntaxNode.Evaluate();
        if (!IsRequiredOption(assignment.Key))
        {
            return;
        }

        if (!assignment.IsValid || assignment.Value is null)
        {
            _hasInvalidRequiredAssignment = true;
            return;
        }

        _values[assignment.Key] = assignment.Value;
    }

    public bool TryCreateSnapshot(out FirewallConfigurationSnapshot? snapshot)
    {
        if (_hasInvalidRequiredAssignment
            || !_values.TryGetValue(IPV6, out string? ipv6Value)
            || !TryParseIPv6(ipv6Value, out bool ipv6Enabled)
            || !_values.TryGetValue(DEFAULT_INPUT_POLICY, out string? inputValue)
            || !TryParsePolicy(inputValue, out FirewallDefaultPolicy incomingPolicy)
            || !_values.TryGetValue(DEFAULT_OUTPUT_POLICY, out string? outputValue)
            || !TryParsePolicy(outputValue, out FirewallDefaultPolicy outgoingPolicy)
            || !_values.TryGetValue(DEFAULT_FORWARD_POLICY, out string? forwardValue)
            || !TryParsePolicy(forwardValue, out FirewallDefaultPolicy routedPolicy))
        {
            snapshot = null;
            return false;
        }

        snapshot = new FirewallConfigurationSnapshot(ipv6Enabled, incomingPolicy, outgoingPolicy, routedPolicy);
        return true;
    }

    private static bool IsRequiredOption(string key) => key is IPV6 or DEFAULT_INPUT_POLICY or DEFAULT_OUTPUT_POLICY or DEFAULT_FORWARD_POLICY;

    private static bool TryParseIPv6(string value, out bool enabled)
    {
        if (value.Equals("yes", StringComparison.OrdinalIgnoreCase))
        {
            enabled = true;
            return true;
        }
        if (value.Equals("no", StringComparison.OrdinalIgnoreCase))
        {
            enabled = false;
            return true;
        }

        enabled = false;
        return false;
    }

    private static bool TryParsePolicy(string value, out FirewallDefaultPolicy policy)
    {
        if (value.Equals("ACCEPT", StringComparison.OrdinalIgnoreCase) || value.Equals("ALLOW", StringComparison.OrdinalIgnoreCase))
        {
            policy = FirewallDefaultPolicy.Allow;
            return true;
        }
        if (value.Equals("DROP", StringComparison.OrdinalIgnoreCase) || value.Equals("DENY", StringComparison.OrdinalIgnoreCase))
        {
            policy = FirewallDefaultPolicy.Deny;
            return true;
        }
        if (value.Equals("REJECT", StringComparison.OrdinalIgnoreCase))
        {
            policy = FirewallDefaultPolicy.Reject;
            return true;
        }

        policy = default;
        return false;
    }
}
