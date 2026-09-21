namespace Ufw.Web.Client.Features.Rules.Metadata;

internal static class RuleTagColor
{
    public static bool TryNormalize(string? value, out string color)
    {
        string candidate = value?.Trim() ?? string.Empty;
        if (candidate.Length != 7 || candidate[0] != '#')
        {
            color = string.Empty;
            return false;
        }

        for (int index = 1; index < candidate.Length; index++)
        {
            if (!Uri.IsHexDigit(candidate[index]))
            {
                color = string.Empty;
                return false;
            }
        }

        color = candidate.ToUpperInvariant();
        return true;
    }
}
