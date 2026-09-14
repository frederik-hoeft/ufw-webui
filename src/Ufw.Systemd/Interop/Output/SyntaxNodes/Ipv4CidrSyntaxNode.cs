using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Output.Visitors;

namespace Ufw.Systemd.Interop.Output.SyntaxNodes;

internal sealed class Ipv4CidrSyntaxNode(string? name, string sourceAddress) : SyntaxNodeBase<IUfwListCommandResultRowVisitor, string>(name, sourceAddress)
{
    protected override void Accept(IUfwListCommandResultRowVisitor visitor) => visitor.Visit(this);
}
