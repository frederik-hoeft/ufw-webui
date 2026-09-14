using System.Text;
using Ufw.Shared.Parsing.Visitors;

namespace Ufw.Shared.Parsing.SyntaxNodes;

public sealed class AlternativeSyntaxNode : SyntaxNodeBase
{
    private readonly ISyntaxNode _inner;

    public AlternativeSyntaxNode(string? name, ISyntaxNode inner) : base(name)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
        _inner.Parent = this;
    }

    public override void Accept(INodeVisitor visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        _inner.Accept(visitor);
    }

    public override void ToString(StringBuilder builder, int indentLevel)
    {
        base.ToString(builder, indentLevel);
        _inner.ToString(builder, indentLevel + 1);
    }
}
