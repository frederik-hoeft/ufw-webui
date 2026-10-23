using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Iptables.Parsing;

namespace Ufw.Systemd.Interop.Iptables.Parsing.SyntaxNodes;

internal sealed class SectionLineSyntaxNode(string? name, string sectionName) : SyntaxNodeBase<IIptablesDocumentVisitor>(name)
{
    protected override void Accept(IIptablesDocumentVisitor visitor) => visitor.VisitSection(sectionName);
}
