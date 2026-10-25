using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Iptables.Parsing.SyntaxNodes;

namespace Ufw.Systemd.Interop.Iptables.Parsing.Terminals;

internal sealed partial class QuotedStringToken(string? name = null) : RegexParserBase<QuotedStringToken, IIptablesTokenVisitor>(name), IParser<QuotedStringToken>, IRegexOwner
{
    public static QuotedStringToken Instance { get; } = new();

    [GeneratedRegex(@"\G(?<quote>[""'])(?<value>(?:\\.|(?!\k<quote>)[^\\\r\n])*)\k<quote>")]
    public static partial Regex ParserRegex { get; }

    public override IParser NamedCopy(string name) => new QuotedStringToken(name);

    protected override bool TryCreateSyntaxNode(Match match, [NotNullWhen(true)] out ISyntaxNode? syntaxNode)
    {
        string content = QuotedStringEscapeDecoder.Decode(match.Groups["value"].Value);
        syntaxNode = new IptablesTokenSyntaxNode(Name, IptablesTokenKind.QuotedString, content);
        return true;
    }
}
