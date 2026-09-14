using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing.SyntaxNodes;

namespace Ufw.Shared.Parsing.Parsers;

public sealed class Whitespace(string? name = null) : ParserBase, IParser<Whitespace>
{
    public static Whitespace Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new Whitespace(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        if ((uint)offset > (uint)input.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        int index = offset;
        while (index < input.Length && char.IsWhiteSpace(input[index]))
        {
            index++;
        }
        int consumed = index - offset;
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
