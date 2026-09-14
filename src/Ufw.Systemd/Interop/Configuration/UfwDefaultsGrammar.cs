using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Firewall;
using Ufw.Shared.Parsing;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Configuration.Parsers;
using Ufw.Systemd.Interop.Configuration.Visitors;

namespace Ufw.Systemd.Interop.Configuration;

internal sealed class UfwDefaultsGrammar
{
    private readonly IParser _grammar;

    private UfwDefaultsGrammar()
    {
        _grammar = Grammar.Repeat<UfwDefaultsLine>().RequireVisitor<IUfwDefaultsVisitor>();
    }

    public static UfwDefaultsGrammar Instance { get; } = new();

    public bool TryParse(string input, [NotNullWhen(true)] out FirewallConfigurationSnapshot? snapshot)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!_grammar.TryParse(input, 0, out ISyntaxNode? syntaxNode, out int charsConsumed) || charsConsumed != input.Length)
        {
            snapshot = null;
            return false;
        }

        UfwDefaultsVisitor visitor = new();
        syntaxNode.Accept(visitor);
        return visitor.TryCreateSnapshot(out snapshot);
    }
}
