using Ufw.Shared.Parsing.Visitors;
using Ufw.Shared.Firewall.Iptables.Parsing.SyntaxNodes;

namespace Ufw.Shared.Firewall.Iptables.Parsing;

internal interface IUfwTupleVisitor : INodeVisitor
{
    void Visit(UfwTupleActionSyntaxNode node);
    void Visit(UfwTupleProtocolSyntaxNode node);
    void Visit(UfwTuplePortSyntaxNode node);
    void Visit(UfwTupleAddressSyntaxNode node);
    void Visit(UfwTupleApplicationSyntaxNode node);
    void Visit(UfwTupleDirectionSyntaxNode node);
    void Visit(UfwTupleCommentSyntaxNode node);
}
