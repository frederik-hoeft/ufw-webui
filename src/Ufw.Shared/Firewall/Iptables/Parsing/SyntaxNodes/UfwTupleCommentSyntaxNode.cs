using Ufw.Shared.Parsing.SyntaxNodes;

namespace Ufw.Shared.Firewall.Iptables.Parsing.SyntaxNodes;

internal sealed class UfwTupleCommentSyntaxNode(string? name, string value) : SyntaxNodeBase<IUfwTupleVisitor>(name)
{
    public string Value { get; } = value;
    protected override void Accept(IUfwTupleVisitor visitor) => visitor.Visit(this);
}
