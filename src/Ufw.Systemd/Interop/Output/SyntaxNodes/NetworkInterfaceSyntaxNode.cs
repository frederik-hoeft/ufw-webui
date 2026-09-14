using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Output.Visitors;

namespace Ufw.Systemd.Interop.Output.SyntaxNodes;

internal sealed class NetworkInterfaceSyntaxNode(string? name, string networkInterface) : SyntaxNodeBase<IUfwListCommandResultRowVisitor, string>(name, networkInterface)
{
    protected override void Accept(IUfwListCommandResultRowVisitor visitor) => visitor.Visit(this);
}
