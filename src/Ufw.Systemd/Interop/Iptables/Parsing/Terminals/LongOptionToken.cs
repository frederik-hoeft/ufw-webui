using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Iptables.Parsing;

namespace Ufw.Systemd.Interop.Iptables.Parsing.Terminals;

internal sealed class LongOptionToken(string? name = null) : ParserBase<IIptablesTokenVisitor>, IParser<LongOptionToken>
{
    public static LongOptionToken Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new LongOptionToken(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        return IptablesTerminal.TryTake(input, offset, static text =>
        {
            if (text.Length < 3 || text[0] != '-' || text[1] != '-')
            {
                return 0;
            }

            int length = 2 + IptablesTerminal.MatchWhile(text[2..], static character => char.IsAsciiLetterOrDigit(character) || character == '-');
            return length > 2 ? length : 0;
        }, IptablesTokenKind.LongOption, name, out syntaxNode, out charsConsumed);
    }
}
