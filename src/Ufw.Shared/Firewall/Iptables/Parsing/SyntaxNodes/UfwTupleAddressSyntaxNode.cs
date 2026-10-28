using Ufw.Shared.Parsing.SyntaxNodes;

namespace Ufw.Shared.Firewall.Iptables.Parsing.SyntaxNodes;

internal sealed class UfwTupleAddressSyntaxNode(string? name, UfwTupleFieldRole role, string value) : SyntaxNodeBase<IUfwTupleVisitor>(name)
{
    public UfwTupleFieldRole Role { get; } = role;
    public string Value { get; } = value;
    protected override void Accept(IUfwTupleVisitor visitor) => visitor.Visit(this);
}
