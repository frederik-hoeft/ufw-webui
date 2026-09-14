using System.Text;
using Ufw.Shared.Parsing.Visitors;

namespace Ufw.Shared.Parsing.SyntaxNodes;

public sealed class RepeatedSyntaxNode : SyntaxNodeBase
{
    private readonly ISyntaxNode[] _nodes;

    public RepeatedSyntaxNode(string? name, IReadOnlyList<ISyntaxNode> nodes) : base(name)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        _nodes = [.. nodes];
        foreach (ISyntaxNode node in _nodes)
        {
            node.Parent = this;
        }
    }

    public override void Accept(INodeVisitor visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        foreach (ISyntaxNode node in _nodes)
        {
            node.Accept(visitor);
        }
    }

    public override void ToString(StringBuilder builder, int indentLevel)
    {
        base.ToString(builder, indentLevel);
        foreach (ISyntaxNode node in _nodes)
        {
            node.ToString(builder, indentLevel + 1);
        }
    }
}
