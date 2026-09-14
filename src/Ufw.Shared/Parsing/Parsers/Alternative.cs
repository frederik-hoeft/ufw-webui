using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing.SyntaxNodes;

namespace Ufw.Shared.Parsing.Parsers;

public class Alternative(ImmutableArray<IParser> parsers, string? name = null) : ParserBase
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

    public override IParser NamedCopy(string name) => new Alternative(parsers, name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        if ((uint)offset > (uint)input.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

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
