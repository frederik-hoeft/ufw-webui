using System.Text;
using Ufw.Systemd.Interop.Output.Visitors;

namespace Ufw.Systemd.Interop.Output.SyntaxNodes;

internal sealed class WhitespaceSyntaxNode(string? name, int length) : SyntaxNodeBase(name)
{
    public override void Accept(INodeVisitor visitor) { }

    public override void ToString(StringBuilder builder, int indentLevel)
    {
        builder.Append(' ', indentLevel * 2);
        builder.Append(GetType().Name);
        if (Name is not null)
        {
            builder.Append($" (Name: '{Name}' Length: {length})");
        }
        builder.AppendLine();
    }
}
