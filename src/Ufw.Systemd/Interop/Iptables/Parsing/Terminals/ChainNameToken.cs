using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Iptables.Parsing;

namespace Ufw.Systemd.Interop.Iptables.Parsing.Terminals;

internal sealed class ChainNameToken(string? name = null) : ParserBase<IIptablesTokenVisitor>, IParser<ChainNameToken>
{
    public static ChainNameToken Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new ChainNameToken(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        return IptablesTerminal.TryTake(input, offset, static text => IptablesTerminal.MatchWhile(text, static character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-'), IptablesTokenKind.ChainName, name, out syntaxNode, out charsConsumed);
    }
}
