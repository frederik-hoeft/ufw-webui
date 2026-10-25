using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Iptables.Parsing;
using Ufw.Systemd.Interop.Iptables.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Iptables.Parsing.Terminals;

namespace Ufw.Systemd.Interop.Iptables.Parsing.Lines;

internal sealed class IptablesTupleLine(string? name = null) : ParserBase<IIptablesDocumentVisitor>, IParser<IptablesTupleLine>
{
    private const string PREFIX = "### tuple ###";

    public static IptablesTupleLine Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new IptablesTupleLine(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!TryReadLine(input, offset, out ReadOnlySpan<char> content, out int consumed) || !content.StartsWith(PREFIX))
        {
            syntaxNode = null;
            charsConsumed = 0;
            return false;
        }

        string body = content[PREFIX.Length..].Trim().ToString();
        syntaxNode = new TupleLineSyntaxNode(name, body);
        charsConsumed = consumed;
        return true;
    }

    internal static bool TryReadLine(string input, int offset, out ReadOnlySpan<char> content, out int consumed)
    {
        if ((uint)offset > (uint)input.Length)
        {
            content = default;
            consumed = 0;
            return false;
        }

        int index = offset;
        while (index < input.Length && input[index] is not ('\r' or '\n'))
        {
            index++;
        }

        if (index == offset)
        {
            content = default;
            consumed = 0;
            return false;
        }

        content = input.AsSpan(offset, index - offset);
        if (content[^1] == '\r')
        {
            content = content[..^1];
        }

        if (RequiredLineEnding.Instance.TryParse(input, index, out _, out int endingLength))
        {
            index += endingLength;
        }

        consumed = index - offset;
        return content.Length > 0;
    }
}
