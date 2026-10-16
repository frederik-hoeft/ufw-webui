using Ufw.Shared.Parsing.Visitors;
using Ufw.Systemd.Interop.Iptables.Parsing.SyntaxNodes;

namespace Ufw.Systemd.Interop.Iptables.Parsing;

internal interface IIptablesTokenVisitor : INodeVisitor
{
    void Visit(IptablesTokenSyntaxNode syntaxNode);
}
