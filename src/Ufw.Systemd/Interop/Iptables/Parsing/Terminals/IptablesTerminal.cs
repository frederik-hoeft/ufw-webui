using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Iptables.Parsing.SyntaxNodes;

namespace Ufw.Systemd.Interop.Iptables.Parsing.Terminals;

/// <summary>
/// Shared scan helpers for iptables terminals. Each terminal parser still recognizes one token.
/// </summary>
internal static class IptablesTerminal
{
    public static bool TryTake(string input, int offset, Func<ReadOnlySpan<char>, int> match, IptablesTokenKind kind, string? name, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        if ((uint)offset > (uint)input.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        int length = match(input.AsSpan(offset));
        if (length <= 0)
        {
            syntaxNode = null;
            charsConsumed = 0;
            return false;
        }

        syntaxNode = new IptablesTokenSyntaxNode(name, kind, input.Substring(offset, length));
        charsConsumed = length;
        return true;
    }

    public static int MatchWhile(ReadOnlySpan<char> text, Func<char, bool> predicate)
    {
        int index = 0;
        while (index < text.Length && predicate(text[index]))
        {
            index++;
        }

        return index;
    }
}
