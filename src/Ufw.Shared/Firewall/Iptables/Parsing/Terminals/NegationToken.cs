using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Shared.Firewall.Iptables.Parsing;

namespace Ufw.Shared.Firewall.Iptables.Parsing.Terminals;

internal sealed class NegationToken(string? name = null) : ParserBase<IIptablesTokenVisitor>, IParser<NegationToken>
{
    public static NegationToken Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new NegationToken(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        return IptablesTerminal.TryTake(input, offset, static text => text.Length > 0 && text[0] == '!' ? 1 : 0, IptablesTokenKind.Negation, name, out syntaxNode, out charsConsumed);
    }
}
