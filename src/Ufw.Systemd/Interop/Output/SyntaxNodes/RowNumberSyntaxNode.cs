using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Output.Visitors;

namespace Ufw.Systemd.Interop.Output.SyntaxNodes;

internal sealed class RowNumberSyntaxNode(string? name, int value) : SyntaxNodeBase<IUfwListCommandResultRowVisitor, int>(name, value)
{
    protected override void Accept(IUfwListCommandResultRowVisitor visitor) => visitor.Visit(this);
}
