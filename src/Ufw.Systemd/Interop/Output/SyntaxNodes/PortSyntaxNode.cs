using Ufw.Systemd.Interop.Output.Visitors;

namespace Ufw.Systemd.Interop.Output.SyntaxNodes;

internal sealed class PortSyntaxNode(string? name, string ports) : SyntaxNodeBase<string>(name, ports)
{
    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}
