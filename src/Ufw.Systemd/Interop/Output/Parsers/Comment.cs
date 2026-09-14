using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Output.SyntaxNodes;
using Ufw.Systemd.Interop.Output.Visitors;

namespace Ufw.Systemd.Interop.Output.Parsers;

internal sealed class Comment(string? name = null) : ParserBase<IUfwListCommandResultRowVisitor>, IParser<Comment>
{
    public static Comment Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new Comment(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
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
