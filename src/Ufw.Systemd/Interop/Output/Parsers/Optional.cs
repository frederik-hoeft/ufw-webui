using System.Diagnostics.CodeAnalysis;
using Ufw.Systemd.Interop.Output.SyntaxNodes;

namespace Ufw.Systemd.Interop.Output.Parsers;

internal class Optional(IParser optionalParser, string? name = null) : IParser
{
    public string? Name => name;

    public IParser NamedCopy(string name) => new Optional(optionalParser, name);

    public bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        if (optionalParser.TryParse(input, offset, out ISyntaxNode? inner, out int innerConsumed))
        {
            syntaxNode = new OptionalSyntaxNode(name, inner);
            charsConsumed = innerConsumed;
            return true;
        }
        syntaxNode = name is null ? OptionalSyntaxNode.Instance : new OptionalSyntaxNode(name, inner: null);
        charsConsumed = 0;
        return true;
    }
}