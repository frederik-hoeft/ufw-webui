using Ufw.Shared.Firewall;
using Ufw.Shared.Parsing.SyntaxNodes;

namespace Ufw.Shared.Firewall.Iptables.Parsing.SyntaxNodes;

internal sealed class UfwTupleActionSyntaxNode(string? name, FirewallAction action, bool forward, UfwTupleLogging logging) : SyntaxNodeBase<IUfwTupleVisitor>(name)
{
    public FirewallAction Action { get; } = action;
    public bool Forward { get; } = forward;
    public UfwTupleLogging Logging { get; } = logging;
    protected override void Accept(IUfwTupleVisitor visitor) => visitor.Visit(this);
}
