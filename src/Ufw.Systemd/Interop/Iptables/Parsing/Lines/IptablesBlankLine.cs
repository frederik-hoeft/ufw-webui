using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Iptables.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Iptables.Parsing.Terminals;
using Ufw.Systemd.Interop.Iptables.Parsing;

namespace Ufw.Systemd.Interop.Iptables.Parsing.Lines;

internal sealed class IptablesBlankLine(string? name = null) : ParserBase
{
    public static IptablesBlankLine Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new IptablesBlankLine(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        int index = offset;
        while (index < input.Length && input[index] is ' ' or '\t')
        {
            index++;
        }

        if (!RequiredLineEnding.Instance.TryParse(input, index, out _, out int endingLength))
        {
            syntaxNode = null;
            charsConsumed = 0;
            return false;
        }

        syntaxNode = new IgnoredIptablesLineSyntaxNode(name);
        charsConsumed = index - offset + endingLength;
        return true;
    }
}
