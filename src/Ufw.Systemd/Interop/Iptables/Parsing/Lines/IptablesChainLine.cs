using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Iptables.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Iptables.Parsing.Terminals;
using Ufw.Systemd.Interop.Iptables.Parsing;

namespace Ufw.Systemd.Interop.Iptables.Parsing.Lines;

internal sealed class IptablesChainLine : ParserBase<IIptablesDocumentVisitor>
{
    private readonly IParser _parser;

    private IptablesChainLine(string? name)
    {
        Name = name;
        _parser = Grammar.Sequence(
            new LiteralCharacterParser(':'),
            ChainNameToken.Instance.NamedCopy("chain-name"),
            HorizontalWhitespace.Instance,
            ChainPolicyToken.Instance.NamedCopy("chain-policy"),
            HorizontalWhitespace.Instance,
            CounterToken.Instance.NamedCopy("chain-counter"),
            Grammar.Optional(HorizontalWhitespace.Instance),
            EndOfLine.Instance);
    }

    public static IptablesChainLine Instance { get; } = new(name: null);

    public override string? Name { get; }

    public override IParser NamedCopy(string name) => new IptablesChainLine(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!_parser.TryParse(input, offset, out ISyntaxNode? node, out charsConsumed))
        {
            syntaxNode = null;
            return false;
        }

        IptablesTokenCollector collector = new();
        node.Accept(collector);
        string? chainName = null;
        string? policy = null;
        foreach (IptablesToken token in collector.Tokens)
        {
            if (token.Role == "chain-name")
            {
                chainName = token.Text;
            }
            else if (token.Role == "chain-policy")
            {
                policy = token.Text;
            }
        }

        if (chainName is null || policy is null)
        {
            syntaxNode = null;
            charsConsumed = 0;
            return false;
        }

        syntaxNode = new ChainLineSyntaxNode(Name, chainName, policy);
        return true;
    }
}
