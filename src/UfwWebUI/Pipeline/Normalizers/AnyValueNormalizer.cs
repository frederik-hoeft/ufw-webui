using UfwWebUI.Models;

namespace UfwWebUI.Pipeline.Normalizers;

internal sealed class AnyValueNormalizer : IRuleNormalizer
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
        if (string.IsNullOrWhiteSpace(value) || value.Equals("any", StringComparison.OrdinalIgnoreCase))
        {
            return "any";
        }
        return value;
    }
}
