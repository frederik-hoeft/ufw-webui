using UfwWebUI.Models;

namespace UfwWebUI.Pipeline.Normalizers;

internal sealed class TrimWhitespaceNormalizer : IRuleNormalizer
{
    public int Priority => 1; // Run first to trim whitespace

    public void Normalize(UfwRule rule)
    {
        // Trim whitespace from all string fields
        rule.Source = TrimOrNull(rule.Source);
        rule.Target = TrimOrNull(rule.Target);
        rule.Ports = TrimOrNull(rule.Ports);
        rule.Interface = TrimOrNull(rule.Interface);
        rule.Comment = TrimOrNull(rule.Comment);
    }

    private static string? TrimOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        ReadOnlySpan<char> span = value.AsSpan().Trim();
        return span.IsEmpty ? null : span.ToString();
    }
}
