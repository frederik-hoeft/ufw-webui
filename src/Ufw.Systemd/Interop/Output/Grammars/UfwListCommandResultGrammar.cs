using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Ufw.Systemd.Interop.Output.Model;
using Ufw.Systemd.Interop.Output.Parsers;
using Ufw.Systemd.Interop.Output.SyntaxNodes;
using Ufw.Systemd.Interop.Output.Visitors;

namespace Ufw.Systemd.Interop.Output.Grammars;

internal sealed class UfwListCommandResultGrammar
{
    private IParser UfwRuleListGrammar { get; }

    [SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "That would be horrible to read.")]
    public UfwListCommandResultGrammar()
    {
        // ENDPOINT = (Anywhere|IPv4CIDR[ port]|port)[proto][ interface]
        IParser endpoint = Sequence<
            Alternative<
                Anywhere,
                Sequence<Ipv4Cidr, Optional<Sequence<Whitespace, PortSegment>>>,
                PortSegment>,
            Optional<Protocol>,
            Optional<Sequence<Whitespace, NetworkInterface>>>.Instance;

        UfwRuleListGrammar = Grammar.Sequence(sequence => sequence
            .Parser<RowNumber>()
            .Parser<Whitespace>()
            .Parser(endpoint.NamedCopy(DestinationGroup))
            .Parser<Whitespace>()
            .Parser<RoutingAction>()
            .Parser<Whitespace>()
            .Parser(endpoint.NamedCopy(SourceGroup))
            .Parser<Whitespace>()
            .Parser<Optional<Sequence<OutHint, Whitespace>>>()
            .Parser<Optional<Sequence<CommentStart, Alternative<JsonComment, Comment>>>>());
    }

    internal static string SourceGroup => "source";

    internal static string DestinationGroup => "destination";

    public static UfwListCommandResultGrammar Instance { get; } = new();

    public bool TryParse(string input, [NotNullWhen(true)] out UfwListCommandResultRow? result)
    {
        if (!UfwRuleListGrammar.TryParse(input, 0, out ISyntaxNode? node, out _))
        {
            result = null;
            return false;
        }
        string s = node.ToString();
        Debug.WriteLine(s);
        result = new UfwListCommandResultRow();
        UfwListCommandResultRowVisitor visitor = new(result);
        node.Accept(visitor);
        return true;
    }
}
