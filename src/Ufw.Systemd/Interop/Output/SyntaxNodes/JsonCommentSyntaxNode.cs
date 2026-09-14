using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Output.Model;
using Ufw.Systemd.Interop.Output.Visitors;

namespace Ufw.Systemd.Interop.Output.SyntaxNodes;

internal sealed class JsonCommentSyntaxNode(string? name, UfwRuleContext context) : SyntaxNodeBase<IUfwListCommandResultRowVisitor, UfwRuleContext>(name, context)
{
    protected override void Accept(IUfwListCommandResultRowVisitor visitor) => visitor.Visit(this);
}
