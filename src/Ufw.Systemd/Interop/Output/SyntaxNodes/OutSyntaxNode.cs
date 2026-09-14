using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Output.Visitors;

namespace Ufw.Systemd.Interop.Output.SyntaxNodes;

internal sealed class OutSyntaxNode(string? name) : SyntaxNodeBase<IUfwListCommandResultRowVisitor>(name)
{

    protected override void Accept(IUfwListCommandResultRowVisitor visitor) => visitor.Visit(this);
}
