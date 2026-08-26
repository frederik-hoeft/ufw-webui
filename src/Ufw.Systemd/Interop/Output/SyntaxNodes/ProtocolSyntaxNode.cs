using Ufw.Systemd.Interop.Output.Model;
using Ufw.Systemd.Interop.Output.Visitors;

namespace Ufw.Systemd.Interop.Output.SyntaxNodes;

internal sealed class ProtocolSyntaxNode(string? name, UfwProtocol protocol) : SyntaxNodeBase<UfwProtocol>(name, protocol)
{
    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}
