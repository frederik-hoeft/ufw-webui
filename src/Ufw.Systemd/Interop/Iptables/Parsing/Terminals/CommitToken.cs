using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Iptables.Parsing;

namespace Ufw.Systemd.Interop.Iptables.Parsing.Terminals;

internal sealed class CommitToken(string? name = null) : ParserBase<IIptablesTokenVisitor>, IParser<CommitToken>
{
    public static CommitToken Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new CommitToken(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        return IptablesTerminal.TryTake(input, offset, static text => text.StartsWith("COMMIT") && (text.Length == 6 || !char.IsAsciiLetterOrDigit(text[6])) ? 6 : 0, IptablesTokenKind.Commit, name, out syntaxNode, out charsConsumed);
    }
}
