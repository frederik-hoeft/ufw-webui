using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Shared.Firewall.Iptables.Parsing;

namespace Ufw.Shared.Firewall.Iptables.Parsing.SyntaxNodes;

internal sealed class SectionLineSyntaxNode(string? name, string sectionName) : SyntaxNodeBase<IIptablesDocumentVisitor>(name)
{
    protected override void Accept(IIptablesDocumentVisitor visitor) => visitor.VisitSection(sectionName);
}
