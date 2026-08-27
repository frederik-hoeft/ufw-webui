using System.Text;
using Ufw.Systemd.Interop.Output.Visitors;

namespace Ufw.Systemd.Interop.Output.SyntaxNodes;

internal sealed class OptionalSyntaxNode : SyntaxNodeBase
{
    private readonly ISyntaxNode? _inner;

    public OptionalSyntaxNode(string? name, ISyntaxNode? inner) : base(name)
    {
        _inner = inner;
        _inner?.Parent = this;
    }

    public static OptionalSyntaxNode Instance { get; } = new(name: null, inner: null);

    public override void Accept(INodeVisitor visitor) => _inner?.Accept(visitor);

    public override void ToString(StringBuilder builder, int indentLevel)
    {
        base.ToString(builder, indentLevel);
        _inner?.ToString(builder, indentLevel + 1);
    }
}
