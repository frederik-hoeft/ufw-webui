using Ufw.Systemd.Interop.Output.Model;
using Ufw.Systemd.Interop.Output.Visitors;

namespace Ufw.Systemd.Interop.Output.SyntaxNodes;

internal sealed class JsonCommentSyntaxNode(string? name, UfwRuleContext context) : SyntaxNodeBase<UfwRuleContext>(name, context)
{
    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}
