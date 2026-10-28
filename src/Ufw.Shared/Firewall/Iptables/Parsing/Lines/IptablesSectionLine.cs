using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Shared.Firewall.Iptables.Parsing;
using Ufw.Shared.Firewall.Iptables.Parsing.SyntaxNodes;
using Ufw.Shared.Firewall.Iptables.Parsing.Terminals;

namespace Ufw.Shared.Firewall.Iptables.Parsing.Lines;

internal sealed class IptablesSectionLine(string? name = null) : ParserBase<IIptablesDocumentVisitor>, IParser<IptablesSectionLine>
{
    public static IptablesSectionLine Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new IptablesSectionLine(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!IptablesTupleLine.TryReadLine(input, offset, out ReadOnlySpan<char> content, out int consumed))
        {
            syntaxNode = null;
            charsConsumed = 0;
            return false;
        }

        ReadOnlySpan<char> trimmed = content.Trim();
        if (!trimmed.StartsWith("###") || !trimmed.EndsWith("###") || trimmed.Length < 7)
        {
            syntaxNode = null;
            charsConsumed = 0;
            return false;
        }

        string section = trimmed[3..^3].Trim().ToString();
        if (section.Length == 0 || section.Contains("###", StringComparison.Ordinal))
        {
            syntaxNode = null;
            charsConsumed = 0;
            return false;
        }

        syntaxNode = new SectionLineSyntaxNode(name, section);
        charsConsumed = consumed;
        return true;
    }
}
