using System.Diagnostics.CodeAnalysis;
using Ufw.Systemd.Interop.Output.SyntaxNodes;

namespace Ufw.Systemd.Interop.Output.Parsers;

internal sealed class Comment(string? name = null) : IParser<Comment>
{
    public static Comment Instance { get; } = new();

    public string? Name => name;

    public IParser NamedCopy(string name) => new Comment(name);

    public bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ReadOnlySpan<char> inputSpan = input.AsSpan(offset);
        if (!inputSpan.IsEmpty && !inputSpan.IsWhiteSpace())
        {
            string comment = inputSpan.ToString();
            charsConsumed = inputSpan.Length;
            syntaxNode = new CommentSyntaxNode(Name, comment);
            return true;
        }
        charsConsumed = 0;
        syntaxNode = null;
        return false;
    }
}