using Ufw.Systemd.Interop.Output.Visitors;

namespace Ufw.Systemd.Interop.Output.SyntaxNodes;

internal sealed class AnywhereSyntaxNode(string? name) : SyntaxNodeBase(name)
{
    public static AnywhereSyntaxNode Instance { get; } = new(name: null);

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}
