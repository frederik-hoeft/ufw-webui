using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;

namespace Ufw.Systemd.Interop.Iptables.Parsing.Terminals;

/// <summary>The literal '-' field used by UFW application-profile tuple metadata.</summary>
internal sealed class TuplePlaceholderToken(string? name = null) : ParserBase<IIptablesTokenVisitor>, IParser<TuplePlaceholderToken>
{
    public static TuplePlaceholderToken Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new TuplePlaceholderToken(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed) =>
        IptablesTerminal.TryTake(input, offset,
            static text => text.Length > 0 && text[0] == '-' && (text.Length == 1 || char.IsWhiteSpace(text[1])) ? 1 : 0,
            IptablesTokenKind.BareWord, name, out syntaxNode, out charsConsumed);
}
