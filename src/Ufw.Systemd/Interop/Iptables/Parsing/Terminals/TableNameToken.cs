using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Iptables.Parsing;

namespace Ufw.Systemd.Interop.Iptables.Parsing.Terminals;

internal sealed class TableNameToken(string? name = null) : ParserBase<IIptablesTokenVisitor>, IParser<TableNameToken>
{
    public static TableNameToken Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new TableNameToken(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        if ((uint)offset >= (uint)input.Length || input[offset] != '*')
        {
            syntaxNode = null;
            charsConsumed = 0;
            return false;
        }

        int length = IptablesTerminal.MatchWhile(input.AsSpan(offset + 1), static character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');
        if (length == 0)
        {
            syntaxNode = null;
            charsConsumed = 0;
            return false;
        }

        syntaxNode = new SyntaxNodes.IptablesTokenSyntaxNode(name, IptablesTokenKind.TableName, input.Substring(offset + 1, length));
        charsConsumed = length + 1;
        return true;
    }
}
