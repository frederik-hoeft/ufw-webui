using Ufw.Systemd.Interop.Output.Model;
using Ufw.Systemd.Interop.Output.Visitors;

namespace Ufw.Systemd.Interop.Output.SyntaxNodes;

internal sealed class ActionSyntaxNode(string? name, RuleType ruleType, Direction direction) : SyntaxNodeBase<FirewallAction>(name, new FirewallAction(ruleType, direction))
{
    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}
