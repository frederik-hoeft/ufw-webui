using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Output.SyntaxNodes;

namespace Ufw.Systemd.Interop.Output.Parsers;

internal sealed partial class CommentStart(string? name = null) : ParserBase, IParser<CommentStart>
{
    public static CommentStart Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new CommentStart(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        const string COMMENT_START = "# ";
        ReadOnlySpan<char> inputSpan = input.AsSpan(offset);
        if (inputSpan.StartsWith(COMMENT_START, StringComparison.Ordinal))
        {
            charsConsumed = COMMENT_START.Length;
            syntaxNode = new CommentStartSyntaxNode(Name);
            return true;
        }
        charsConsumed = 0;
        syntaxNode = null;
        return false;
    }
}
