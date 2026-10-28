using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Shared.Firewall.Iptables.Parsing.SyntaxNodes;
using Ufw.Shared.Firewall.Iptables.Parsing.Terminals;

namespace Ufw.Shared.Firewall.Iptables.Parsing.Lines;

/// <summary>Recognizes an iptables-restore flush command for one chain.</summary>
internal sealed class IptablesFlushLine : ParserBase<IIptablesDocumentVisitor>
{
    private readonly IParser _parser;

    private IptablesFlushLine(string? name)
    {
        Name = name;
        _parser = Grammar.Sequence(
            ShortOptionToken.Instance.NamedCopy("flush-command"),
            HorizontalWhitespace.Instance,
            ChainNameToken.Instance.NamedCopy("flush-chain"),
            Grammar.Optional(HorizontalWhitespace.Instance),
            EndOfLine.Instance);
    }

    public static IptablesFlushLine Instance { get; } = new(name: null);

    public override string? Name { get; }

    public override IParser NamedCopy(string name) => new IptablesFlushLine(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        if (!_parser.TryParse(input, offset, out ISyntaxNode? node, out charsConsumed))
        {
            syntaxNode = null;
            return false;
        }

        IptablesTokenCollector collector = new();
        node.Accept(collector);
        if (collector.Tokens is not [{ Role: "flush-command", Text: "-F" }, { Role: "flush-chain" } chain])
        {
            syntaxNode = null;
            charsConsumed = 0;
            return false;
        }

        syntaxNode = new FlushLineSyntaxNode(Name, chain.Text);
        return true;
    }
}
