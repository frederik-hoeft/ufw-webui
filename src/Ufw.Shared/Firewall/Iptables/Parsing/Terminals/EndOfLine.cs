using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Shared.Firewall.Iptables.Parsing.SyntaxNodes;

namespace Ufw.Shared.Firewall.Iptables.Parsing.Terminals;

/// <summary>Matches a line ending, or succeeds without consuming characters at end of input.</summary>
internal sealed class EndOfLine(string? name = null) : ParserBase
{
    public static EndOfLine Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new EndOfLine(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        if ((uint)offset > (uint)input.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        if (offset == input.Length)
        {
            syntaxNode = new WhitespaceSyntaxNode(name, 0);
            charsConsumed = 0;
            return true;
        }

        int length = 0;
        if (input[offset] == '\r')
        {
            length++;
        }

        if (offset + length >= input.Length || input[offset + length] != '\n')
        {
            syntaxNode = null;
            charsConsumed = 0;
            return false;
        }

        syntaxNode = new WhitespaceSyntaxNode(name, length + 1);
        charsConsumed = length + 1;
        return true;
    }
}
