using System.Text;
using Ufw.Web.Models;

namespace Ufw.Web.Pipeline.Normalizers;

internal sealed class PortRangeNormalizer : IRuleNormalizer
{
    public int Priority => 3; // Run after any normalization

    public void Normalize(UfwRule rule)
    {
        if (string.IsNullOrWhiteSpace(rule.Ports))
        {
            return;
        }
        // Remove all whitespace from port ranges using span for efficiency
        rule.Ports = RemoveWhitespace(rule.Ports);
    }

    private static string RemoveWhitespace(string input)
    {
        ReadOnlySpan<char> span = input.AsSpan();
        
        // Check if there's any whitespace first
        bool hasWhitespace = false;
        foreach (char c in span)
        {
            if (char.IsWhiteSpace(c))
            {
                hasWhitespace = true;
                break;
            }
        }

        if (!hasWhitespace)
        {
            return input; // Return original if no whitespace
        }

        // Use StringBuilder only if needed
        StringBuilder sb = new(span.Length);
        foreach (char c in span)
        {
            if (!char.IsWhiteSpace(c))
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}
