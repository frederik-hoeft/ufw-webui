using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing.SyntaxNodes;

namespace Ufw.Shared.Parsing.Parsers;

public class Repeat : ParserBase
{
    private readonly IParser _parser;
    private readonly int _minimumCount;
    private readonly string? _name;

    public Repeat(IParser parser, int minimumCount = 0, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentOutOfRangeException.ThrowIfNegative(minimumCount);
        _parser = parser;
        _minimumCount = minimumCount;
        _name = name;
    }

    public override string? Name => _name;

    public override bool CanAccept(Type visitorType)
    {
        ArgumentNullException.ThrowIfNull(visitorType);
        return _parser.CanAccept(visitorType);
    }

    public override IParser NamedCopy(string name) => new Repeat(_parser, _minimumCount, name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        if ((uint)offset > (uint)input.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        List<ISyntaxNode> nodes = [];
        int currentOffset = offset;
        while (currentOffset < input.Length && _parser.TryParse(input, currentOffset, out ISyntaxNode? node, out int consumed))
        {
            if (consumed <= 0)
            {
                throw new InvalidOperationException($"Repeated parser '{_parser.Name ?? _parser.GetType().Name}' succeeded without consuming input.");
            }
            nodes.Add(node);
            currentOffset += consumed;
        }

        if (nodes.Count < _minimumCount)
        {
            syntaxNode = null;
            charsConsumed = 0;
            return false;
        }

        syntaxNode = new RepeatedSyntaxNode(_name, nodes);
        charsConsumed = currentOffset - offset;
        return true;
    }
}
