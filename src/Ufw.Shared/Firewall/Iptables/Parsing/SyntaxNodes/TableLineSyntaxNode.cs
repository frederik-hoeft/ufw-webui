using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Shared.Firewall.Iptables.Parsing;

namespace Ufw.Shared.Firewall.Iptables.Parsing.SyntaxNodes;

internal sealed class TableLineSyntaxNode(string? name, string tableName) : SyntaxNodeBase<IIptablesDocumentVisitor>(name)
{
    protected override void Accept(IIptablesDocumentVisitor visitor) => visitor.VisitTable(tableName);
}
