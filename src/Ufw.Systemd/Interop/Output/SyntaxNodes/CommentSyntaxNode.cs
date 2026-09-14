using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Output.Visitors;

namespace Ufw.Systemd.Interop.Output.SyntaxNodes;

internal sealed class CommentSyntaxNode(string? name, string comment) : SyntaxNodeBase<IUfwListCommandResultRowVisitor, string>(name, comment)
{
    protected override void Accept(IUfwListCommandResultRowVisitor visitor) => visitor.Visit(this);
}
