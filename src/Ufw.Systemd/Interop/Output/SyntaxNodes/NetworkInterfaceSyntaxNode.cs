using Ufw.Systemd.Interop.Output.Visitors;

namespace Ufw.Systemd.Interop.Output.SyntaxNodes;

internal sealed class NetworkInterfaceSyntaxNode(string? name, string networkInterface) : SyntaxNodeBase<string>(name, networkInterface)
{
    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}
