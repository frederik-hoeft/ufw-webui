using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Output.Model;
using Ufw.Systemd.Interop.Output.Visitors;

namespace Ufw.Systemd.Interop.Output.SyntaxNodes;

internal sealed class ProtocolSyntaxNode(string? name, UfwProtocol protocol) : SyntaxNodeBase<IUfwListCommandResultRowVisitor, UfwProtocol>(name, protocol)
{
    protected override void Accept(IUfwListCommandResultRowVisitor visitor) => visitor.Visit(this);
}
