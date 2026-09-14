using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing.SyntaxNodes;

namespace Ufw.Shared.Parsing.Parsers;

public class Sequence(ImmutableArray<IParser> parsers, string? name = null) : ParserBase
{
    public override string? Name => name;

    public override bool CanAccept(Type visitorType)
    {
        ArgumentNullException.ThrowIfNull(visitorType);
        foreach (IParser parser in parsers)
        {
            if (!parser.CanAccept(visitorType))
            {
                return false;
            }
        }
        return true;
    }

    public override IParser NamedCopy(string name) => new Sequence(parsers, name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        if ((uint)offset > (uint)input.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

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
            tail = tail is null ? node : new SequentialSyntaxNode(tail, node, Name);
        }
        if (tail is null)
        {
            charsConsumed = 0;
            syntaxNode = null;
            return false;
        }
        charsConsumed = currentOffset - offset;
        syntaxNode = tail;
        return true;
    }
}
