using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Ufw.Systemd.Interop.Output.SyntaxNodes;

namespace Ufw.Systemd.Interop.Output.Parsers;

internal abstract class RegexParserBase<TSelf>(string? name) : IParser where TSelf : RegexParserBase<TSelf>, IRegexOwner
{
    public string? Name { get; init; } = name;

    protected abstract bool TryCreateSyntaxNode(Match match, [NotNullWhen(true)] out ISyntaxNode? syntaxNode);

    public virtual bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        if (TSelf.ParserRegex.IsMatch(input.AsSpan(offset))                       // zero-alloc pre-check
            && TSelf.ParserRegex.Match(input, offset) is { Success: true } match  // should never fail
            && TryCreateSyntaxNode(match, out syntaxNode))
        {
            charsConsumed = match.Length;
            return true;
        }
        charsConsumed = 0;
        syntaxNode = null;
        return false;
    }

    public abstract IParser NamedCopy(string name);
}
