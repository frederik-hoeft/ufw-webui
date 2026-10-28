using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Shared.Firewall.Iptables.Parsing;
using Ufw.Shared.Firewall.Iptables.Parsing.Terminals;

namespace Ufw.Shared.Firewall.Iptables.Grammars;

/// <summary>
/// Composes supported UFW tuple layouts from typed field parsers. All source-specific
/// syntax decisions happen here, not in the specification-mapping layer.
/// </summary>
internal sealed class UfwTupleBodyGrammar
{
    private readonly IParser _grammar;

    private UfwTupleBodyGrammar()
    {
        IParser ws = HorizontalWhitespace.Instance;
        IParser core = Grammar.Sequence(
            UfwTupleAction.Instance, ws, UfwTupleProtocol.Instance, ws,
            new UfwTuplePort("destination-port", UfwTupleFieldRole.Destination), ws,
            new UfwTupleAddress("destination-address", UfwTupleFieldRole.Destination), ws,
            new UfwTuplePort("source-port", UfwTupleFieldRole.Source), ws,
            new UfwTupleAddress("source-address", UfwTupleFieldRole.Source));

        IParser appPair = Grammar.Sequence(
            new UfwTupleApplication("destination-application", UfwTupleFieldRole.Destination), ws,
            new UfwTupleApplication("source-application", UfwTupleFieldRole.Source));

        _grammar = Grammar.Sequence(
            core,
            Grammar.Optional(Grammar.Alternative(
                Grammar.Sequence(ws, appPair, ws, UfwTupleDirection.Instance),
                Grammar.Sequence(ws, UfwTupleDirection.Instance),
                Grammar.Sequence(ws, appPair))),
            Grammar.Optional(Grammar.Sequence(ws, UfwTupleComment.Instance)),
            Grammar.Optional(ws))
            .RequireVisitor<IUfwTupleVisitor>();
    }

    public static UfwTupleBodyGrammar Instance { get; } = new();

    public bool TryParse(string input, [NotNullWhen(true)] out UfwTupleSyntax? tuple)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!_grammar.TryParse(input, 0, out ISyntaxNode? node, out int consumed) || consumed != input.Length)
        {
            tuple = null;
            return false;
        }

        UfwTupleSyntaxCollector collector = new();
        node.Accept(collector);
        tuple = collector.Build();
        return true;
    }
}
