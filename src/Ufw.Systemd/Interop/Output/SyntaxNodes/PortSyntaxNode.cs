using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Output.Visitors;

namespace Ufw.Systemd.Interop.Output.SyntaxNodes;

internal sealed class PortSyntaxNode(string? name, string ports) : SyntaxNodeBase<IUfwListCommandResultRowVisitor, string>(name, ports)
{
    protected override void Accept(IUfwListCommandResultRowVisitor visitor) => visitor.Visit(this);
}
