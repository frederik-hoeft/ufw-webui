using Ufw.Shared.Parsing.SyntaxNodes;

namespace Ufw.Systemd.Interop.Iptables.Parsing.SyntaxNodes;

internal sealed class UfwTupleProtocolSyntaxNode(string? name, string value) : SyntaxNodeBase<IUfwTupleVisitor>(name)
{
    public string Value { get; } = value;
    protected override void Accept(IUfwTupleVisitor visitor) => visitor.Visit(this);
}
