using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Ufw.Shared.Parsing.SyntaxNodes;

namespace Ufw.Shared.Parsing.Parsers;

public abstract class RegexParserBase<TSelf>(string? name) : ParserBase
    where TSelf : RegexParserBase<TSelf>, IRegexOwner
{
    public override string? Name { get; } = name;

    protected abstract bool TryCreateSyntaxNode(Match match, [NotNullWhen(true)] out ISyntaxNode? syntaxNode);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        if ((uint)offset > (uint)input.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        if (TSelf.ParserRegex.IsMatch(input.AsSpan(offset))
            && TSelf.ParserRegex.Match(input, offset) is { Success: true } match
            && TryCreateSyntaxNode(match, out syntaxNode))
        {
            charsConsumed = match.Length;
            return true;
        }
        charsConsumed = 0;
        syntaxNode = null;
        return false;
    }
}
