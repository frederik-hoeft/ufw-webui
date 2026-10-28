using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;

namespace Ufw.Systemd.Interop.Iptables.Parsing.Terminals;

/// <summary>The -I command used in UFW translation-file support sections, but never in iptables-save output.</summary>
internal sealed class InsertCommand(string? name = null) : ParserBase<IIptablesTokenVisitor>, IParser<InsertCommand>
{
    public static InsertCommand Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new InsertCommand(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        return IptablesTerminal.TryTake(input, offset, static text => text.StartsWith("-I") && (text.Length == 2 || !IsNameCharacter(text[2])) ? 2 : 0,
            IptablesTokenKind.Append, name, out syntaxNode, out charsConsumed);
    }

    private static bool IsNameCharacter(char character) => char.IsAsciiLetterOrDigit(character) || character is '_' or '-';
}
