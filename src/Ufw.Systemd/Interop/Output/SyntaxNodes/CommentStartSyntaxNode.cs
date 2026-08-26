using Ufw.Systemd.Interop.Output.Visitors;

namespace Ufw.Systemd.Interop.Output.SyntaxNodes;

internal sealed class CommentStartSyntaxNode(string? name) : SyntaxNodeBase(name)
{
    public static CommentStartSyntaxNode Instance { get; } = new(name: null);

    public override void Accept(INodeVisitor visitor) { }
}
