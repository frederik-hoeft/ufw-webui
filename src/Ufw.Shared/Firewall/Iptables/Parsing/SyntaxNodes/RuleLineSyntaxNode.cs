using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Shared.Firewall.Iptables.Parsing;

namespace Ufw.Shared.Firewall.Iptables.Parsing.SyntaxNodes;

internal sealed class RuleLineSyntaxNode(string? name, string chainName, IReadOnlyList<IptablesToken> arguments, string sourceLine, bool insert) : SyntaxNodeBase<IIptablesDocumentVisitor>(name)
{
    protected override void Accept(IIptablesDocumentVisitor visitor) => visitor.VisitRule(chainName, arguments, sourceLine, insert);
}
