using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Ufw.Systemd.Interop.Output.SyntaxNodes;

namespace Ufw.Systemd.Interop.Output.Parsers;

internal class Sequence(ImmutableArray<IParser> parsers, string? name = null) : IParser
{
    public string? Name => name;

    public IParser NamedCopy(string name) => new Sequence(parsers, name);

    public bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        int currentOffset = offset;
        ISyntaxNode? tail = null;
        foreach (IParser parser in parsers)
        {
            if (!parser.TryParse(input, currentOffset, out ISyntaxNode? node, out int consumed))
            {
                charsConsumed = 0;
                syntaxNode = null;
                return false;
            }
            currentOffset += consumed;
            if (tail is null)
            {
                tail = node;
            }
            else
            {
                tail = new SequentialSyntaxNode(tail, node, Name);
            }
        }
        if (tail is null)
        {
            charsConsumed = 0;
            syntaxNode = null;
            return false;
        }
        charsConsumed = currentOffset - offset;
        syntaxNode = tail!;
        return true;
    }
}
