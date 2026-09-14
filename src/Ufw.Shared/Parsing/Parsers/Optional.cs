using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing.SyntaxNodes;

namespace Ufw.Shared.Parsing.Parsers;

public class Optional(IParser optionalParser, string? name = null) : ParserBase
{
    public override string? Name => name;

    public override bool CanAccept(Type visitorType)
    {
        ArgumentNullException.ThrowIfNull(visitorType);
        return optionalParser.CanAccept(visitorType);
    }

    public override IParser NamedCopy(string name) => new Optional(optionalParser, name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        if ((uint)offset > (uint)input.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        if (optionalParser.TryParse(input, offset, out ISyntaxNode? inner, out int innerConsumed))
        {
            syntaxNode = new OptionalSyntaxNode(name, inner);
            charsConsumed = innerConsumed;
            return true;
        }
        syntaxNode = new OptionalSyntaxNode(name, inner: null);
        charsConsumed = 0;
        return true;
    }
}
