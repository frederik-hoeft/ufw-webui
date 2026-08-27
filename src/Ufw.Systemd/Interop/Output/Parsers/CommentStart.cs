using System.Diagnostics.CodeAnalysis;
using Ufw.Systemd.Interop.Output.SyntaxNodes;

namespace Ufw.Systemd.Interop.Output.Parsers;

internal sealed partial class CommentStart(string? name = null) : IParser<CommentStart>
{
    public static CommentStart Instance { get; } = new();

    public string? Name => name;

    public IParser NamedCopy(string name) => new CommentStart(name);

    public bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        const string COMMENT_START = "# ";
        ReadOnlySpan<char> inputSpan = input.AsSpan(offset);
        if (inputSpan.StartsWith(COMMENT_START, StringComparison.Ordinal))
        {
            charsConsumed = COMMENT_START.Length;
            syntaxNode = Name is null ? CommentStartSyntaxNode.Instance : new CommentStartSyntaxNode(Name);
            return true;
        }
        charsConsumed = 0;
        syntaxNode = null;
        return false;
    }
}
