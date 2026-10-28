using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Shared.Firewall.Iptables.Parsing;

namespace Ufw.Shared.Firewall.Iptables.Parsing.Terminals;

internal sealed class AppendCommand(string? name = null) : ParserBase<IIptablesTokenVisitor>, IParser<AppendCommand>
{
    public static AppendCommand Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new AppendCommand(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        return IptablesTerminal.TryTake(input, offset, static text => text.StartsWith("-A") && (text.Length == 2 || !IsNameCharacter(text[2])) ? 2 : 0, IptablesTokenKind.Append, name, out syntaxNode, out charsConsumed);
    }

    private static bool IsNameCharacter(char character) => char.IsAsciiLetterOrDigit(character) || character is '_' or '-';
}
