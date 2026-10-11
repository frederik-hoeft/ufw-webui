using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing.SyntaxNodes;

namespace Ufw.Shared.Parsing.Parsers;

/// <summary>
/// Matches a nonempty subset of child parsers in any order. Each child position can match at most once;
/// zero-width matches are ignored without claiming a member position, allowing that member to match later.
/// At least one member must consume input. Consuming matches are greedy in declaration order, without backtracking.
/// </summary>
public class Set(ImmutableArray<IParser> parsers, string? name = null) : ParserBase
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

    public override IParser NamedCopy(string name) => new Set(parsers, name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        if ((uint)offset > (uint)input.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        bool[] usedParsers = new bool[parsers.Length];
        List<ISyntaxNode> nodes = [];
        int currentOffset = offset;

        // Restart after every match so earlier members can match after a later member advances the offset.
        // Track positions rather than parser instances so repeated instances remain distinct grammar members.
        bool matched;
        do
        {
            matched = false;
            for (int i = 0; i < parsers.Length; i++)
            {
                if (usedParsers[i] || !parsers[i].TryParse(input, currentOffset, out ISyntaxNode? node, out int consumed))
                {
                    continue;
                }

                // A zero-width success leaves this member available at later offsets.
                if (consumed == 0)
                {
                    continue;
                }

                usedParsers[i] = true;
                nodes.Add(node);
                currentOffset += consumed;
                matched = true;
                break;
            }
        } while (matched);

        if (nodes.Count == 0)
        {
            charsConsumed = 0;
            syntaxNode = null;
            return false;
        }

        charsConsumed = currentOffset - offset;
        syntaxNode = new SetSyntaxNode(name, nodes);
        return true;
    }
}
