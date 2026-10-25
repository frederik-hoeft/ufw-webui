using Ufw.Shared.Parsing.SyntaxNodes;

namespace Ufw.Systemd.Interop.Iptables.Parsing.SyntaxNodes;

internal sealed class UfwTupleDirectionSyntaxNode(string? name, string direction, string interfaceIn, string interfaceOut) : SyntaxNodeBase<IUfwTupleVisitor>(name)
{
    public string Direction { get; } = direction;
    public string InterfaceIn { get; } = interfaceIn;
    public string InterfaceOut { get; } = interfaceOut;
    protected override void Accept(IUfwTupleVisitor visitor) => visitor.Visit(this);
}
