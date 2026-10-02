using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Output.Model;
using Ufw.Systemd.Interop.Output.SyntaxNodes;
using Ufw.Systemd.Interop.Output.Visitors;

namespace Ufw.Systemd.Interop.Output.Parsers;

internal sealed class EndpointRoleParser(IParser inner, EndpointRole role, string? name = null) : ParserBase<IUfwListCommandResultRowVisitor>
{
    public override string? Name => name;

    public override bool CanAccept(Type visitorType) => base.CanAccept(visitorType) && inner.CanAccept(visitorType);

    public override IParser NamedCopy(string name) => new EndpointRoleParser(inner, role, name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!inner.TryParse(input, offset, out ISyntaxNode? endpoint, out charsConsumed))
        {
            syntaxNode = null;
            return false;
        }

        syntaxNode = new EndpointSyntaxNode(role, endpoint);
        return true;
    }
}
