using UfwWebUI.Models;

namespace UfwWebUI.Pipeline.Normalizers;

public sealed class AnyValueNormalizer : IRuleNormalizer
{
    public int Priority => 2; // Run after trimming

    public void Normalize(UfwRule rule)
    {
        // Normalize "any" to lowercase and fill blank values
        rule.Source = NormalizeAny(rule.Source);
        rule.Target = NormalizeAny(rule.Target);
    }

    private static string NormalizeAny(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "any";
        }

        // Use span for efficient comparison
        ReadOnlySpan<char> span = value.AsSpan();
        if (span.Length == 3 &&
            (span[0] == 'a' || span[0] == 'A') &&
            (span[1] == 'n' || span[1] == 'N') &&
            (span[2] == 'y' || span[2] == 'Y'))
        {
            return "any";
        }

        return value;
    }
}
