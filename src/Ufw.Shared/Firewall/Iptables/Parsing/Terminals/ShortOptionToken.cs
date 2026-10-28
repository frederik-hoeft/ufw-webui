using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Shared.Firewall.Iptables.Parsing;

namespace Ufw.Shared.Firewall.Iptables.Parsing.Terminals;

internal sealed class ShortOptionToken(string? name = null) : ParserBase<IIptablesTokenVisitor>, IParser<ShortOptionToken>
{
    public static ShortOptionToken Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new ShortOptionToken(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        return IptablesTerminal.TryTake(input, offset, static text => text.Length >= 2 && text[0] == '-' && char.IsAsciiLetterOrDigit(text[1]) ? 2 : 0, IptablesTokenKind.ShortOption, name, out syntaxNode, out charsConsumed);
    }
}
