using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Iptables.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Iptables.Parsing.Terminals;
using Ufw.Systemd.Interop.Iptables.Parsing;

namespace Ufw.Systemd.Interop.Iptables.Parsing.Lines;

internal sealed class IptablesRuleLine : ParserBase<IIptablesDocumentVisitor>
{
    private static readonly IParser s_argument = Grammar.Alternative(
        NegationToken.Instance.NamedCopy("rule-argument"),
        LongOptionToken.Instance.NamedCopy("rule-argument"),
        ShortOptionToken.Instance.NamedCopy("rule-argument"),
        QuotedStringToken.Instance.NamedCopy("rule-argument"),
        BareWordToken.Instance.NamedCopy("rule-argument"));

    private readonly IParser _parser;

    private IptablesRuleLine(string? name)
    {
        Name = name;
        _parser = Grammar.Sequence(
            AppendCommand.Instance,
            HorizontalWhitespace.Instance,
            ChainNameToken.Instance.NamedCopy("rule-chain"),
            Grammar.Optional(Grammar.Sequence(HorizontalWhitespace.Instance, CounterToken.Instance.NamedCopy("rule-counter"))),
            Grammar.Repeat(Grammar.Sequence(HorizontalWhitespace.Instance, s_argument)),
            Grammar.Optional(HorizontalWhitespace.Instance),
            EndOfLine.Instance);
    }

    public static IptablesRuleLine Instance { get; } = new(name: null);

    public override string? Name { get; }

    public override IParser NamedCopy(string name) => new IptablesRuleLine(name);

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
        List<IptablesToken> arguments = [];
        foreach (IptablesToken token in collector.Tokens)
        {
            if (token.Role == "rule-chain")
            {
                chainName = token.Text;
            }
            else if (token.Role == "rule-argument")
            {
                arguments.Add(token);
            }
        }

        if (chainName is null)
        {
            syntaxNode = null;
            charsConsumed = 0;
            return false;
        }

        syntaxNode = new RuleLineSyntaxNode(Name, chainName, arguments, input.Substring(offset, charsConsumed).TrimEnd('\r', '\n'));
        return true;
    }
}
