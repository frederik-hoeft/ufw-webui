namespace Ufw.Client.Components.Rules;

internal sealed record RuleMatchTextSnippet(
    string Prefix,
    string Match,
    string Suffix,
    bool HasLeadingEllipsis,
    bool HasTrailingEllipsis)
{
    public static RuleMatchTextSnippet Create(string value, int start, int length, int contextLength = 20)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (start < 0 || length < 0 || start > value.Length - length)
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }
        if (contextLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(contextLength));
        }

        int prefixStart = Math.Max(0, start - contextLength);
        int suffixEnd = Math.Min(value.Length, start + length + contextLength);
        return new RuleMatchTextSnippet(
            value[prefixStart..start],
            value.Substring(start, length),
            value[(start + length)..suffixEnd],
            prefixStart > 0,
            suffixEnd < value.Length);
    }
}
