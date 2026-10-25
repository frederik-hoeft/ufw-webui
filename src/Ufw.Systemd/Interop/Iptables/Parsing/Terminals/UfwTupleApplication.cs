using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Iptables.Parsing.SyntaxNodes;

namespace Ufw.Systemd.Interop.Iptables.Parsing.Terminals;

internal sealed partial class UfwTupleApplication(string? name, UfwTupleFieldRole role) : RegexParserBase<UfwTupleApplication, IUfwTupleVisitor>(name), IRegexOwner
{
    // Profile names may be quoted, and '-' denotes an absent profile.
    [GeneratedRegex("\\G(?:\"(?<double>(?:\\\\.|[^\"\\\\])*)\"|'(?<single>(?:\\\\.|[^'\\\\])*)'|(?<plain>[^\"'\\s]+))(?=\\s|$)")]
    public static partial Regex ParserRegex { get; }

    public override IParser NamedCopy(string name) => new UfwTupleApplication(name, role);

    protected override bool TryCreateSyntaxNode(Match match, [NotNullWhen(true)] out ISyntaxNode? syntaxNode)
    {
        bool doubleQuoted = match.Groups["double"].Success;
        bool singleQuoted = match.Groups["single"].Success;
        string value = doubleQuoted ? match.Groups["double"].Value :
            singleQuoted ? match.Groups["single"].Value : match.Groups["plain"].Value;
        syntaxNode = new UfwTupleApplicationSyntaxNode(Name, role, doubleQuoted || singleQuoted ? QuotedStringEscapeDecoder.Decode(value) : value);
        return true;
    }
}
