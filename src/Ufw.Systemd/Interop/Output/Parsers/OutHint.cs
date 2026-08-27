using System.Diagnostics.CodeAnalysis;
using Ufw.Systemd.Interop.Output.SyntaxNodes;

namespace Ufw.Systemd.Interop.Output.Parsers;

internal sealed partial class OutHint(string? name = null) : IParser<OutHint>
{
    public static OutHint Instance { get; } = new();

    public string? Name => name;

    public IParser NamedCopy(string name) => new OutHint(name);

    public bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        const string OUT = "(out)";
        ReadOnlySpan<char> inputSpan = input.AsSpan(offset);
        if (inputSpan.StartsWith(OUT, StringComparison.Ordinal))
        {
            charsConsumed = OUT.Length;
            syntaxNode = Name is null ? OutSyntaxNode.Instance : new OutSyntaxNode(Name);
            return true;
        }
        charsConsumed = 0;
        syntaxNode = null;
        return false;
    }
}
