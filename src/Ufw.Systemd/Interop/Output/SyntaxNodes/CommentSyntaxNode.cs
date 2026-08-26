using Ufw.Systemd.Interop.Output.Visitors;

namespace Ufw.Systemd.Interop.Output.SyntaxNodes;

internal sealed class CommentSyntaxNode(string? name, string comment) : SyntaxNodeBase<string>(name, comment)
{
    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}
