using Ufw.Systemd.Interop.Iptables.Parsing.SyntaxNodes;

namespace Ufw.Systemd.Interop.Iptables.Parsing;

internal sealed class IptablesTokenCollector : IIptablesTokenVisitor
{
    private readonly List<IptablesToken> _tokens = [];

    public IReadOnlyList<IptablesToken> Tokens => _tokens;

    public void Visit(IptablesTokenSyntaxNode syntaxNode)
    {
        ArgumentNullException.ThrowIfNull(syntaxNode);
        _tokens.Add(new IptablesToken(syntaxNode.Kind, syntaxNode.Text, syntaxNode.Name));
    }
}
