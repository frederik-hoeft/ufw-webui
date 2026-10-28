using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Shared.Firewall.Iptables.Parsing;
using Ufw.Shared.Firewall.Iptables.Parsing.SyntaxNodes;
using Ufw.Shared.Firewall.Iptables.Parsing.Terminals;

namespace Ufw.Shared.Firewall.Iptables.Parsing.Lines;

internal sealed class IptablesTableLine : ParserBase<IIptablesDocumentVisitor>
{
    private readonly IParser _parser;

    private IptablesTableLine(string? name)
    {
        Name = name;
        _parser = Grammar.Sequence(
            TableNameToken.Instance.NamedCopy("table-name"),
            Grammar.Optional(HorizontalWhitespace.Instance),
            EndOfLine.Instance);
    }

    public static IptablesTableLine Instance { get; } = new(name: null);

    public override string? Name { get; }

    public override IParser NamedCopy(string name) => new IptablesTableLine(name);

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
        if (collector.Tokens.Count != 1 || collector.Tokens[0].Kind != IptablesTokenKind.TableName)
        {
            syntaxNode = null;
            charsConsumed = 0;
            return false;
        }

        syntaxNode = new TableLineSyntaxNode(Name, collector.Tokens[0].Text);
        return true;
    }
}
