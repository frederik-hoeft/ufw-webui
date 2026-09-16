using System.Text;

namespace Ufw.Client.Rules.Filtering;

public sealed record RuleQuery(IReadOnlyList<string> TextTerms, IReadOnlyList<RuleFilter> Filters)
{
    public static RuleQuery Empty { get; } = new([], []);

    public bool IsActive => TextTerms.Count > 0 || Filters.Count > 0;

    public static RuleQuery Create(string? searchText, IEnumerable<RuleFilter>? filters = null) => new(ParseTerms(searchText), filters?.ToArray() ?? []);

    private static IReadOnlyList<string> ParseTerms(string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return [];
        }

        List<string> terms = [];
        StringBuilder current = new();
        bool quoted = false;
        foreach (char character in searchText.Trim())
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
        return terms;
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
