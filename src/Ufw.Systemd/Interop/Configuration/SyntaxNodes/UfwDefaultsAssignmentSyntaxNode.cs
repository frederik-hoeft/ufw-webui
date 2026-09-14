using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Configuration.Visitors;

namespace Ufw.Systemd.Interop.Configuration.SyntaxNodes;

internal sealed class UfwDefaultsAssignmentSyntaxNode(string? name, UfwDefaultsAssignment assignment)
    : SyntaxNodeBase<IUfwDefaultsVisitor, UfwDefaultsAssignment>(name, assignment)
{
    protected override void Accept(IUfwDefaultsVisitor visitor) => visitor.Visit(this);
}
