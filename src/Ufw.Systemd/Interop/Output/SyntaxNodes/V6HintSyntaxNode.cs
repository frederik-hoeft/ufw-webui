using Ufw.Systemd.Interop.Output.Visitors;

namespace Ufw.Systemd.Interop.Output.SyntaxNodes;

internal sealed class V6HintSyntaxNode(string? name) : SyntaxNodeBase(name)
{
    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}
