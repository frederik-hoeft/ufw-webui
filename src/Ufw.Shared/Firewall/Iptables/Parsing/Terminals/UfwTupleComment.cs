using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using Ufw.Shared.Firewall;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Shared.Firewall.Iptables.Parsing.SyntaxNodes;

namespace Ufw.Shared.Firewall.Iptables.Parsing.Terminals;

internal sealed partial class UfwTupleComment(string? name = null) : RegexParserBase<UfwTupleComment, IUfwTupleVisitor>(name), IParser<UfwTupleComment>, IRegexOwner
{
    public static UfwTupleComment Instance { get; } = new();

    [GeneratedRegex(@"\Gcomment=(?<hex>[0-9a-fA-F]*)(?=\s|$)")]
    public static partial Regex ParserRegex { get; }

    public override IParser NamedCopy(string name) => new UfwTupleComment(name);

    protected override bool TryCreateSyntaxNode(Match match, [NotNullWhen(true)] out ISyntaxNode? syntaxNode)
    {
        string hex = match.Groups["hex"].Value;
        if ((hex.Length & 1) != 0)
        {
            syntaxNode = null;
            return false;
        }

        syntaxNode = new UfwTupleCommentSyntaxNode(Name, RuleSpecificationNormalizer.NormalizeComment(Encoding.UTF8.GetString(Convert.FromHexString(hex))) ?? string.Empty);
        return true;
    }
}
