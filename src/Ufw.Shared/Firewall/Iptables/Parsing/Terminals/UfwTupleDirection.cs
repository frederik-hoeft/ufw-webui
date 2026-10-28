using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Shared.Firewall.Iptables.Parsing.SyntaxNodes;

namespace Ufw.Shared.Firewall.Iptables.Parsing.Terminals;

internal sealed partial class UfwTupleDirection(string? name = null) : RegexParserBase<UfwTupleDirection, IUfwTupleVisitor>(name), IParser<UfwTupleDirection>, IRegexOwner
{
    public static UfwTupleDirection Instance { get; } = new();

    [GeneratedRegex(@"\G(?:in_(?<in>[^!\s]+)!out_(?<out>[^!\s]+)|(?<direction>in|out)(?:_(?<interface>[^!\s]+))?)(?=\s|$)")]
    public static partial Regex ParserRegex { get; }

    public override IParser NamedCopy(string name) => new UfwTupleDirection(name);

    protected override bool TryCreateSyntaxNode(Match match, [NotNullWhen(true)] out ISyntaxNode? syntaxNode)
    {
        string direction = match.Groups["direction"].Success ? match.Groups["direction"].Value : "in";
        string single = match.Groups["interface"].Value;
        string ingress = match.Groups["in"].Success ? match.Groups["in"].Value : direction == "in" ? single : string.Empty;
        string egress = match.Groups["out"].Success ? match.Groups["out"].Value : direction == "out" ? single : string.Empty;
        syntaxNode = new UfwTupleDirectionSyntaxNode(Name, direction, ingress, egress);
        return true;
    }
}
