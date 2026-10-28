using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Shared.Firewall.Iptables.Parsing.SyntaxNodes;

namespace Ufw.Shared.Firewall.Iptables.Parsing.Terminals;

/// <summary>Matches one or more horizontal whitespace characters.</summary>
internal sealed class HorizontalWhitespace(string? name = null) : ParserBase, IParser<HorizontalWhitespace>
{
    public static HorizontalWhitespace Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new HorizontalWhitespace(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        int length = IptablesTerminal.MatchWhile(input.AsSpan(offset), static character => character is ' ' or '\t');
        if (length == 0)
        {
            syntaxNode = null;
            charsConsumed = 0;
            return false;
        }

        syntaxNode = new WhitespaceSyntaxNode(name, length);
        charsConsumed = length;
        return true;
    }
}
