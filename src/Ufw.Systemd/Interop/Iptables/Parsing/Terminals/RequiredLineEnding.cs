using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Iptables.Parsing.SyntaxNodes;

using Ufw.Systemd.Interop.Iptables.Parsing;

namespace Ufw.Systemd.Interop.Iptables.Parsing.Terminals;

internal sealed class RequiredLineEnding(string? name = null) : ParserBase
{
    public static RequiredLineEnding Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new RequiredLineEnding(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        if ((uint)offset >= (uint)input.Length)
        {
            syntaxNode = null;
            charsConsumed = 0;
            return false;
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
