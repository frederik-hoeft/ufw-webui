using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Iptables.Parsing;
using Ufw.Systemd.Interop.Iptables.Parsing.Terminals;

namespace Ufw.Systemd.Interop.Iptables.Grammars;

/// <summary>Lexical grammar for a UFW tuple annotation; interpretation belongs to the tuple interpreter.</summary>
internal sealed class UfwTupleBodyGrammar
{
    private readonly IParser _grammar = Grammar.Sequence(
        Grammar.Repeat(Grammar.Sequence(
            Grammar.Optional(HorizontalWhitespace.Instance),
            Grammar.Alternative(
                BareWordToken.Instance.NamedCopy("tuple-field"),
                QuotedStringToken.Instance.NamedCopy("tuple-field"),
                TuplePlaceholderToken.Instance.NamedCopy("tuple-field")))),
        Grammar.Optional(HorizontalWhitespace.Instance))
        .RequireVisitor<IIptablesTokenVisitor>();

    public static UfwTupleBodyGrammar Instance { get; } = new();

    public bool TryParse(string input, [NotNullWhen(true)] out IReadOnlyList<string>? fields)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!_grammar.TryParse(input, 0, out ISyntaxNode? node, out int consumed) || consumed != input.Length)
        {
            fields = null;
            return false;
        }

        IptablesTokenCollector collector = new();
        node.Accept(collector);
        fields = collector.Tokens.Select(static token => token.Text).ToArray();
        return true;
    }
}
