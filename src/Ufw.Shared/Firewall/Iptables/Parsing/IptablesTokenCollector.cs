using Ufw.Shared.Firewall.Iptables.Parsing.SyntaxNodes;

namespace Ufw.Shared.Firewall.Iptables.Parsing;

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
