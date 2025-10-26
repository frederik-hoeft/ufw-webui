using System.Diagnostics.CodeAnalysis;
using Ufw.Systemd.Interop.Output.SyntaxNodes;

namespace Ufw.Systemd.Interop.Output.Parsers;

internal sealed class Anywhere(string? name = null) : IParser<Anywhere>
{
    public static Anywhere Instance { get; } = new();

    public string? Name => name;

    public IParser NamedCopy(string name) => new Anywhere(name);

    public bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        const string ANYWHERE = "Anywhere";
        ReadOnlySpan<char> inputSpan = input.AsSpan(offset);
        if (inputSpan.StartsWith(ANYWHERE, StringComparison.Ordinal))
        {
            charsConsumed = ANYWHERE.Length;
            syntaxNode = AnywhereSyntaxNode.Instance;
            return true;
        }
        charsConsumed = 0;
        syntaxNode = null;
        return false;
    }
}