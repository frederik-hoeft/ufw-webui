using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Output.SyntaxNodes;
using Ufw.Systemd.Interop.Output.Visitors;

namespace Ufw.Systemd.Interop.Output.Parsers;

internal sealed class V6Hint(string? name = null) : ParserBase<IUfwListCommandResultRowVisitor>, IParser<V6Hint>
{
    private const string MARKER = "(v6)";

    public static V6Hint Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new V6Hint(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        if (input.AsSpan(offset).StartsWith(MARKER, StringComparison.Ordinal))
        {
            syntaxNode = new V6HintSyntaxNode(Name);
            charsConsumed = MARKER.Length;
            return true;
        }

        syntaxNode = null;
        charsConsumed = 0;
        return false;
    }
}
