using System.Diagnostics.CodeAnalysis;
using Ufw.Systemd.Interop.Output.SyntaxNodes;

namespace Ufw.Systemd.Interop.Output.Parsers;

internal sealed class Whitespace(string? name = null) : IParser<Whitespace>
{
    public static Whitespace Instance { get; } = new();

    public string? Name => name;

    public IParser NamedCopy(string name) => new Whitespace(name);

    public bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        int i = offset;
        while (i < input.Length && char.IsWhiteSpace(input[i]))
        {
            i++;
        }
        int consumed = i - offset;
        if (consumed == 0)
        {
            charsConsumed = 0;
            syntaxNode = null;
            return false;
        }
        charsConsumed = consumed;
        syntaxNode = new WhitespaceSyntaxNode(Name, consumed);
        return true;
    }
}