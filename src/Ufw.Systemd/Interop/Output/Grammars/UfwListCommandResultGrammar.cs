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
        IParser endpoint = Sequence<
            Alternative<Anywhere, Sequence<Alternative<Ipv4Cidr, Ipv6Cidr>, Optional<Sequence<Whitespace, PortSegment>>>, PortSegment>,
            Optional<Protocol>,
            Optional<Sequence<Whitespace, V6Hint>>,
            Optional<Sequence<Whitespace, NetworkInterface>>,
            Optional<Sequence<Whitespace, V6Hint>>>.Instance;

        UfwRuleListGrammar = Grammar.Sequence(sequence => sequence
            .Parser<RowNumber>()
            .Parser<Whitespace>()
            .Parser(endpoint.NamedCopy(DestinationGroup))
            .Parser<Whitespace>()
            .Parser<RoutingAction>()
            .Parser<Whitespace>()
            .Parser(endpoint.NamedCopy(SourceGroup))
            .Parser<Optional<Whitespace>>()
            .Parser<Optional<Sequence<OutHint, Optional<Whitespace>>>>()
            .Parser<Optional<Sequence<CommentStart, Alternative<JsonComment, Comment>>>>());
    }

    internal static string SourceGroup => "source";

    internal static string DestinationGroup => "destination";

    public static UfwListCommandResultGrammar Instance { get; } = new();

    public bool TryParse(string input, [NotNullWhen(true)] out UfwListCommandResultRow? result)
    {
        if (!UfwRuleListGrammar.TryParse(input, 0, out ISyntaxNode? node, out int charsConsumed)
            || charsConsumed != input.Length)
        {
            result = null;
            return false;
        }
        Debug.WriteLine(node.ToString());
        result = new UfwListCommandResultRow();
        UfwListCommandResultRowVisitor visitor = new(result);
        node.Accept(visitor);
        return true;
    }
}
