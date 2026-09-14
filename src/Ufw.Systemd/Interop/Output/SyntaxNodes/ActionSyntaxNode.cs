using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Output.Model;
using Ufw.Systemd.Interop.Output.Visitors;

namespace Ufw.Systemd.Interop.Output.SyntaxNodes;

internal sealed class ActionSyntaxNode(string? name, RuleType ruleType, Direction direction) : SyntaxNodeBase<IUfwListCommandResultRowVisitor, FirewallAction>(name, new FirewallAction(ruleType, direction))
{
    protected override void Accept(IUfwListCommandResultRowVisitor visitor) => visitor.Visit(this);
}
