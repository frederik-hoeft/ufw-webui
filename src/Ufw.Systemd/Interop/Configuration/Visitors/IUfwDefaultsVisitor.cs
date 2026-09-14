using Ufw.Shared.Parsing.Visitors;
using Ufw.Systemd.Interop.Configuration.SyntaxNodes;

namespace Ufw.Systemd.Interop.Configuration.Visitors;

internal interface IUfwDefaultsVisitor : INodeVisitor
{
    void Visit(UfwDefaultsAssignmentSyntaxNode syntaxNode);
}
