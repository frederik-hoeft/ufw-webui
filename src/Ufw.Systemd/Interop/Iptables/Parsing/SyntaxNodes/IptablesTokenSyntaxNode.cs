using Ufw.Shared.Parsing.SyntaxNodes;

namespace Ufw.Systemd.Interop.Iptables.Parsing.SyntaxNodes;

internal sealed class IptablesTokenSyntaxNode(string? name, IptablesTokenKind kind, string text) : SyntaxNodeBase<IIptablesTokenVisitor>(name)
{
    public IptablesTokenKind Kind { get; } = kind;

    public string Text { get; } = text;

    protected override void Accept(IIptablesTokenVisitor visitor) => visitor.Visit(this);
}
