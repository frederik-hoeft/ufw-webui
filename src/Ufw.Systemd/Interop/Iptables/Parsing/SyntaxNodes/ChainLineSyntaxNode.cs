using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Iptables.Parsing;

namespace Ufw.Systemd.Interop.Iptables.Parsing.SyntaxNodes;

internal sealed class ChainLineSyntaxNode(string? name, string chainName, string policy) : SyntaxNodeBase<IIptablesDocumentVisitor>(name)
{
    protected override void Accept(IIptablesDocumentVisitor visitor) => visitor.VisitChain(chainName, policy);
}
