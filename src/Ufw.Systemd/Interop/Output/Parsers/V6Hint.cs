using System.Diagnostics.CodeAnalysis;
using Ufw.Systemd.Interop.Output.SyntaxNodes;

namespace Ufw.Systemd.Interop.Output.Parsers;

internal sealed class V6Hint(string? name = null) : IParser<V6Hint>
{
    private const string MARKER = "(v6)";

    public static V6Hint Instance { get; } = new();

    public string? Name => name;

    public IParser NamedCopy(string name) => new V6Hint(name);

    public bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
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
