using Ufw.Systemd.Interop.Output.Visitors;

namespace Ufw.Systemd.Interop.Output.SyntaxNodes;

internal sealed class Ipv6CidrSyntaxNode(string? name, string sourceAddress) : SyntaxNodeBase<string>(name, sourceAddress)
{
    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}
