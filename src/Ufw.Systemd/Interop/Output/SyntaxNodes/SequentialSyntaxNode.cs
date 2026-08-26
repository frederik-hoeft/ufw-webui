using System.Text;
using Ufw.Systemd.Interop.Output.Visitors;

namespace Ufw.Systemd.Interop.Output.SyntaxNodes;

internal sealed class SequentialSyntaxNode : SyntaxNodeBase
{
    private readonly ISyntaxNode _node1;
    private readonly ISyntaxNode _node2;

    public SequentialSyntaxNode(ISyntaxNode node1, ISyntaxNode node2, string? name) : base(name)
    {
        node1.Parent = this;
        node2.Parent = this;
        _node1 = node1;
        _node2 = node2;
    }

    public override void Accept(INodeVisitor visitor)
    {
        _node1.Accept(visitor);
        _node2.Accept(visitor);
    }

    public override void ToString(StringBuilder builder, int indentLevel)
    {
        base.ToString(builder, indentLevel);
        _node1.ToString(builder, indentLevel + 1);
        _node2.ToString(builder, indentLevel + 1);
    }
}
