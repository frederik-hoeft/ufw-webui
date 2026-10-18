using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Iptables.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Iptables.Parsing.Terminals;
using Ufw.Systemd.Interop.Iptables.Parsing;

namespace Ufw.Systemd.Interop.Iptables.Parsing.Lines;

internal sealed class IptablesCommentLine(string? name = null) : ParserBase
{
    public static IptablesCommentLine Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new IptablesCommentLine(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        if ((uint)offset >= (uint)input.Length || input[offset] != '#')
        {
            syntaxNode = null;
            charsConsumed = 0;
            return false;
        }

        int index = offset + 1;
        while (index < input.Length && input[index] is not ('\r' or '\n'))
        {
            index++;
        }

        if (RequiredLineEnding.Instance.TryParse(input, index, out _, out int endingLength))
        {
            index += endingLength;
        }

        syntaxNode = new IgnoredIptablesLineSyntaxNode(name);
        charsConsumed = index - offset;
        return true;
    }
}
