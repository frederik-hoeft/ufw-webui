using Ufw.Shared.Firewall;

namespace Ufw.Systemd.Interop.Configuration;

internal static class UfwDefaultsParser
{
    private const string IPV6 = "IPV6";
    private const string DEFAULT_INPUT_POLICY = "DEFAULT_INPUT_POLICY";
    private const string DEFAULT_OUTPUT_POLICY = "DEFAULT_OUTPUT_POLICY";
    private const string DEFAULT_FORWARD_POLICY = "DEFAULT_FORWARD_POLICY";

    public static bool TryParse(string contents, out FirewallConfigurationSnapshot? snapshot)
    {
        ArgumentNullException.ThrowIfNull(contents);
        snapshot = null;

        Dictionary<string, string> values = new(StringComparer.Ordinal);
        foreach (string rawLine in contents.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            int separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            string key = line[..separator].Trim();
            if (key is not (IPV6 or DEFAULT_INPUT_POLICY or DEFAULT_OUTPUT_POLICY or DEFAULT_FORWARD_POLICY))
            {
                continue;
            }

            if (!TryParseValue(line[(separator + 1)..], out string? value))
            {
                return false;
            }

            values[key] = value;
        }

        if (!values.TryGetValue(IPV6, out string? ipv6Value)
            || !TryParseIPv6(ipv6Value, out bool ipv6Enabled)
            || !values.TryGetValue(DEFAULT_INPUT_POLICY, out string? inputValue)
            || !TryParsePolicy(inputValue, out FirewallDefaultPolicy incomingPolicy)
            || !values.TryGetValue(DEFAULT_OUTPUT_POLICY, out string? outputValue)
            || !TryParsePolicy(outputValue, out FirewallDefaultPolicy outgoingPolicy)
            || !values.TryGetValue(DEFAULT_FORWARD_POLICY, out string? forwardValue)
            || !TryParsePolicy(forwardValue, out FirewallDefaultPolicy routedPolicy))
        {
            return false;
        }

        snapshot = new FirewallConfigurationSnapshot(ipv6Enabled, incomingPolicy, outgoingPolicy, routedPolicy);
        return true;
    }

    private static bool TryParseValue(string rawValue, out string value)
    {
        string candidate = rawValue.Trim();
        if (candidate.Length == 0)
        {
            value = string.Empty;
            return false;
        }

        if (candidate[0] is '\'' or '"')
        {
            char quote = candidate[0];
            int closingQuote = candidate.IndexOf(quote, 1);
            if (closingQuote < 0)
            {
                value = string.Empty;
                return false;
            }

            string trailing = candidate[(closingQuote + 1)..].TrimStart();
            if (trailing.Length > 0 && !trailing.StartsWith('#'))
            {
                value = string.Empty;
                return false;
            }

            value = candidate[1..closingQuote];
            return value.Length > 0;
        }

        int comment = candidate.IndexOf('#');
        value = (comment < 0 ? candidate : candidate[..comment]).Trim();
        return value.Length > 0;
    }

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
