using System.Text;

namespace Ufw.Client.Rules.Filtering.Text;

internal sealed record TextRuleFilter : RuleFilter
{
    public TextRuleFilter(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        Text = text;
        Terms = ParseTerms(text);
        if (Terms.Count == 0)
        {
            throw new ArgumentException("Text filter must contain at least one search term.", nameof(text));
        }
    }

    public string Text { get; }

    public IReadOnlyList<string> Terms { get; }

    private static IReadOnlyList<string> ParseTerms(string text)
    {
        List<string> terms = [];
        StringBuilder current = new();
        bool quoted = false;
        foreach (char character in text.Trim())
        {
            if (character == '"')
            {
                quoted = !quoted;
                continue;
            }
            if (char.IsWhiteSpace(character) && !quoted)
            {
                AddTerm(terms, current);
                continue;
            }
            current.Append(character);
        }
        AddTerm(terms, current);
        return terms.ToArray();
    }

    private static void AddTerm(List<string> terms, StringBuilder current)
    {
        if (current.Length == 0)
        {
            return;
        }
        terms.Add(current.ToString());
        current.Clear();
    }
}
