using System.Text;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Output.Model;
using Ufw.Systemd.Interop.Output.Visitors;

namespace Ufw.Systemd.Interop.Output.SyntaxNodes;

internal sealed class EndpointSyntaxNode : SyntaxNodeBase<IUfwListCommandResultRowVisitor>
{
    private readonly ISyntaxNode _inner;

    public EndpointSyntaxNode(EndpointRole role, ISyntaxNode inner) : base(name: null)
    {
        ArgumentNullException.ThrowIfNull(inner);
        Role = role;
        _inner = inner;
        _inner.Parent = this;
    }

    public EndpointRole Role { get; }

    protected override void Accept(IUfwListCommandResultRowVisitor visitor)
    {
        visitor.EnterEndpoint(Role);
        try
        {
            _inner.Accept(visitor);
        }
        finally
        {
            visitor.ExitEndpoint(Role);
        }
    }

    public override void ToString(StringBuilder builder, int indentLevel)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Append(' ', indentLevel * 2);
        builder.Append(GetType().Name);
        builder.Append($" (Role: '{Role}')");
        builder.AppendLine();
        _inner.ToString(builder, indentLevel + 1);
    }
}
