using Ufw.Shared.Parsing.SyntaxNodes;

namespace Ufw.Shared.Firewall.Iptables.Parsing.SyntaxNodes;

internal sealed class FlushLineSyntaxNode(string? name, string chainName) : SyntaxNodeBase<IIptablesDocumentVisitor>(name)
{
    protected override void Accept(IIptablesDocumentVisitor visitor) => visitor.VisitFlush(chainName);
}
