using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Iptables.Parsing;

namespace Ufw.Systemd.Interop.Iptables.Parsing.Terminals;

internal sealed class ChainPolicyToken(string? name = null) : ParserBase<IIptablesTokenVisitor>, IParser<ChainPolicyToken>
{
    public static ChainPolicyToken Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new ChainPolicyToken(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        return IptablesTerminal.TryTake(input, offset, static text =>
        {
            if (text.StartsWith("ACCEPT"))
            {
                return 6;
            }

            if (text.StartsWith("DROP"))
            {
                return 4;
            }

            if (text.Length > 0 && text[0] == '-' && (text.Length == 1 || !IsPolicyContinuation(text[1])))
            {
                return 1;
            }

            return 0;
        }, IptablesTokenKind.Policy, name, out syntaxNode, out charsConsumed);
    }

    private static bool IsPolicyContinuation(char character) => char.IsAsciiLetterOrDigit(character) || character is '_' or '-';
}
