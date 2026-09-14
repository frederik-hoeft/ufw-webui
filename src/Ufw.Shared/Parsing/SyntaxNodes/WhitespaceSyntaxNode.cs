using System.Text;

namespace Ufw.Shared.Parsing.SyntaxNodes;

public sealed class WhitespaceSyntaxNode(string? name, int length) : SyntaxNodeBase(name)
{
    public override void ToString(StringBuilder builder, int indentLevel)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Append(' ', indentLevel * 2);
        builder.Append(GetType().Name);
        if (Name is not null)
        {
            builder.Append($" (Name: '{Name}' Length: {length})");
        }
        builder.AppendLine();
    }
}
