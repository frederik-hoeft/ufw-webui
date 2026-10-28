using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Shared.Firewall.Iptables.Parsing;
using Ufw.Shared.Firewall.Iptables.Parsing.SyntaxNodes;
using Ufw.Shared.Firewall.Iptables.Parsing.Terminals;

namespace Ufw.Shared.Firewall.Iptables.Parsing.Lines;

internal sealed class IptablesRuleLine : ParserBase<IIptablesDocumentVisitor>
{
    private static readonly IParser s_argument = Grammar.Alternative(
        NegationToken.Instance.NamedCopy("rule-argument"),
        LongOptionToken.Instance.NamedCopy("rule-argument"),
        ShortOptionToken.Instance.NamedCopy("rule-argument"),
        QuotedStringToken.Instance.NamedCopy("rule-argument"),
        BareWordToken.Instance.NamedCopy("rule-argument"));

    private readonly IParser _parser;

    private readonly bool _allowInsert;

    private IptablesRuleLine(string? name, bool allowInsert = false)
    {
        Name = name;
        _allowInsert = allowInsert;
        _parser = Grammar.Sequence(
            allowInsert ? Grammar.Alternative(AppendCommand.Instance.NamedCopy("rule-operation"), InsertCommand.Instance.NamedCopy("rule-operation")) : AppendCommand.Instance.NamedCopy("rule-operation"),
            HorizontalWhitespace.Instance,
            ChainNameToken.Instance.NamedCopy("rule-chain"),
            Grammar.Optional(Grammar.Sequence(HorizontalWhitespace.Instance, CounterToken.Instance.NamedCopy("rule-counter"))),
            Grammar.Repeat(Grammar.Sequence(HorizontalWhitespace.Instance, s_argument)),
            Grammar.Optional(HorizontalWhitespace.Instance),
            EndOfLine.Instance);
    }

    public static IptablesRuleLine Instance { get; } = new(name: null);

    public static IptablesRuleLine AppendOrInsertInstance { get; } = new(name: null, allowInsert: true);

    public override string? Name { get; }

    public override IParser NamedCopy(string name) => new IptablesRuleLine(name, _allowInsert);

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
        bool insert = false;
        List<IptablesToken> arguments = [];
        foreach (IptablesToken token in collector.Tokens)
        {
            if (token.Role == "rule-operation")
            {
                insert = token.Text == "-I";
            }
            else if (token.Role == "rule-chain")
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

        syntaxNode = new RuleLineSyntaxNode(Name, chainName, arguments, input.Substring(offset, charsConsumed).TrimEnd('\r', '\n'), insert);
        return true;
    }
}
