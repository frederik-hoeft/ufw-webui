using System.Text.RegularExpressions;
using Ufw.Shared.Firewall;

namespace Ufw.Systemd.Interop.Output;

/// <summary>Parses effective policies and unnumbered rules from UFW's verbose status output.</summary>
internal static partial class UfwVerboseStatusParser
{
    public static bool TryParse(string output, UfwStatusSnapshot numbered, out (FirewallDefaultPolicy Incoming, FirewallDefaultPolicy Outgoing, FirewallDefaultPolicy Routed)? policies)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(numbered);
        policies = null;
        bool? active = null;
        string? defaults = null;
        bool inRules = false;
        List<string> rules = [];
        foreach (string rawLine in output.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            string line = rawLine.Trim();
            if (line.StartsWith("Status:", StringComparison.OrdinalIgnoreCase))
            {
                string status = line["Status:".Length..].Trim();
                if (active.HasValue || !status.Equals("active", StringComparison.OrdinalIgnoreCase) && !status.Equals("inactive", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                active = status.Equals("active", StringComparison.OrdinalIgnoreCase);
            }
            else if (line.StartsWith("Default:", StringComparison.OrdinalIgnoreCase))
            {
                if (defaults is not null)
                {
                    return false;
                }
                defaults = line["Default:".Length..].Trim();
            }
            else if (line.StartsWith("--", StringComparison.Ordinal) && line.Contains("---", StringComparison.Ordinal))
            {
                inRules = true;
            }
            else if (inRules && line.Length != 0)
            {
                rules.Add(Normalize(line));
            }
        }

        if (active != numbered.Active || rules.Count != numbered.Rules.Count)
        {
            return false;
        }
        for (int index = 0; index < rules.Count; index++)
        {
            if (numbered.Rules[index].DisplayNumber != index + 1 || !string.Equals(rules[index], Normalize(NumberPrefix().Replace(numbered.Rules[index].RawLine, "")), StringComparison.Ordinal))
            {
                return false;
            }
        }
        if (active == false)
        {
            return defaults is null;
        }
        if (defaults is null)
        {
            return false;
        }

        string[] parts = defaults.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 3
            || !TryPolicy(parts[0], "incoming", out FirewallDefaultPolicy incoming)
            || !TryPolicy(parts[1], "outgoing", out FirewallDefaultPolicy outgoing)
            || !TryPolicy(parts[2], "routed", out FirewallDefaultPolicy routed))
        {
            return false;
        }
        policies = (incoming, outgoing, routed);
        return true;
    }

    private static bool TryPolicy(string text, string direction, out FirewallDefaultPolicy policy)
    {
        policy = default;
        string suffix = $" ({direction})";
        if (!text.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        string value = text[..^suffix.Length].Trim();
        return value.ToLowerInvariant() switch
        {
            "allow" => Assign(FirewallDefaultPolicy.Allow, out policy),
            "deny" => Assign(FirewallDefaultPolicy.Deny, out policy),
            "reject" => Assign(FirewallDefaultPolicy.Reject, out policy),
            "disabled" when direction == "routed" => Assign(FirewallDefaultPolicy.Disabled, out policy),
            _ => false,
        };
    }

    private static bool Assign(FirewallDefaultPolicy value, out FirewallDefaultPolicy policy)
    {
        policy = value;
        return true;
    }

    private static string Normalize(string text) => Whitespace().Replace(text.Trim(), " ");

    [GeneratedRegex(@"^\[\s*\d+\]\s*")]
    private static partial Regex NumberPrefix();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
