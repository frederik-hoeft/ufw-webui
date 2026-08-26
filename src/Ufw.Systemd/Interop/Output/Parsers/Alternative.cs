using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Ufw.Systemd.Interop.Output.SyntaxNodes;

namespace Ufw.Systemd.Interop.Output.Parsers;

internal class Alternative(ImmutableArray<IParser> parsers, string? name = null) : IParser
{
    public string? Name => name;

    public IParser NamedCopy(string name) => new Alternative(parsers, name);

    public bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        foreach (IParser parser in parsers)
        {
            if (parser.TryParse(input, offset, out ISyntaxNode? node, out int consumed))
            {
                charsConsumed = consumed;
                syntaxNode = Name is null ? node : new AlternativeSyntaxNode(Name, node);
                return true;
            }
        }
        charsConsumed = 0;
        syntaxNode = null;
        return false;
    }
}