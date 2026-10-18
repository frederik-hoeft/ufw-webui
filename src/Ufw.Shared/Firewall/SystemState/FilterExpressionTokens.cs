namespace Ufw.Shared.Firewall.SystemState;

/// <summary>
/// Compares parsed opaque/target argument tokens. Legacy callers that supply only rendered
/// expressions are supported through a compatibility tokenizer; live grammar paths supply tokens.
/// </summary>
internal static class FilterExpressionTokens
{
    public static IReadOnlyList<string> Of(FilterClause clause) => clause.ParsedTokens ?? Tokenize(clause.Value);

    public static IReadOnlyList<string> Of(FilterTarget target) => target.ParsedArguments ?? Tokenize(target.Arguments);

    private static IReadOnlyList<string> Tokenize(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return [];
        }

        List<string> tokens = [];
        int index = 0;
        while (index < expression.Length)
        {
            while (index < expression.Length && char.IsWhiteSpace(expression[index]))
            {
                index++;
            }

            if (index >= expression.Length)
            {
                break;
            }

            if (expression[index] is '"' or '\'')
            {
                char quote = expression[index++];
                int start = index;
                while (index < expression.Length && expression[index] != quote)
                {
                    index++;
                }

                tokens.Add(expression[start..index]);
                if (index < expression.Length)
                {
                    index++;
                }
                continue;
            }

            int wordStart = index;
            while (index < expression.Length && !char.IsWhiteSpace(expression[index]))
            {
                index++;
            }
            tokens.Add(expression[wordStart..index]);
        }
        return tokens;
    }
}
