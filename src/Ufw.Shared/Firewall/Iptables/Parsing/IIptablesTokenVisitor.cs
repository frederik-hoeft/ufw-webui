using Ufw.Shared.Parsing.Visitors;
using Ufw.Shared.Firewall.Iptables.Parsing.SyntaxNodes;

namespace Ufw.Shared.Firewall.Iptables.Parsing;

internal interface IIptablesTokenVisitor : INodeVisitor
{
    void Visit(IptablesTokenSyntaxNode syntaxNode);
}
