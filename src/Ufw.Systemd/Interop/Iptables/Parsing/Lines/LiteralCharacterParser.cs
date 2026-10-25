using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Iptables.Parsing;
using Ufw.Systemd.Interop.Iptables.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Iptables.Parsing.Terminals;

namespace Ufw.Systemd.Interop.Iptables.Parsing.Lines;

internal sealed class LiteralCharacterParser(char character, string? name = null) : ParserBase
{
    public override string? Name => name;

    public override IParser NamedCopy(string name) => new LiteralCharacterParser(character, name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        if ((uint)offset >= (uint)input.Length || input[offset] != character)
        {
            syntaxNode = null;
            charsConsumed = 0;
            return false;
        }

        syntaxNode = new WhitespaceSyntaxNode(name, 1);
        charsConsumed = 1;
        return true;
    }
}
