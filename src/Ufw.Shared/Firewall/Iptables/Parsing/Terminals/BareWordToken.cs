using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Shared.Firewall.Iptables.Parsing;

namespace Ufw.Shared.Firewall.Iptables.Parsing.Terminals;

internal sealed class BareWordToken(string? name = null) : ParserBase<IIptablesTokenVisitor>, IParser<BareWordToken>
{
    public static BareWordToken Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new BareWordToken(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        return IptablesTerminal.TryTake(input, offset, static text =>
        {
            if (text.Length == 0 || !IsBareStart(text[0]))
            {
                return 0;
            }

            return IptablesTerminal.MatchWhile(text, static character => !char.IsWhiteSpace(character));
        }, IptablesTokenKind.BareWord, name, out syntaxNode, out charsConsumed);
    }

    private static bool IsBareStart(char character) => !char.IsWhiteSpace(character) && character is not ('-' or '!' or '"' or '\'' or '#');
}
