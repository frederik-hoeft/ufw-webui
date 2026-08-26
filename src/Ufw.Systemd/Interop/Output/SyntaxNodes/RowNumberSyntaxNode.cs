using Ufw.Systemd.Interop.Output.Visitors;

namespace Ufw.Systemd.Interop.Output.SyntaxNodes;

internal sealed class RowNumberSyntaxNode(string? name, int value) : SyntaxNodeBase<int>(name, value)
{
    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}
