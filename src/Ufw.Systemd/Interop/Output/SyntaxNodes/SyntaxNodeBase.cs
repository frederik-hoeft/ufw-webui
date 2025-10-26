using System.Text;
using Ufw.Systemd.Interop.Output.Visitors;

namespace Ufw.Systemd.Interop.Output.SyntaxNodes;

internal abstract class SyntaxNodeBase(string? name) : ISyntaxNode
{
    public string? Name => name;

    public ISyntaxNode? Parent { get; set; }

    public abstract void Accept(INodeVisitor visitor);

    public bool HasParent(string name)
    {
        bool found = false;
        for (ISyntaxNode? current = Parent; current is not null && !found; current = current.Parent)
        {
            found = current.Name == name;
        }
        return found;
    }

    public override string ToString()
    {
        StringBuilder stringBuilder = new();
        ToString(stringBuilder, 0);
        return stringBuilder.ToString();
    }

    public virtual void ToString(StringBuilder builder, int indentLevel)
    {
        builder.Append(' ', indentLevel * 2);
        builder.Append(GetType().Name);
        if (Name is not null)
        {
            builder.Append($" (Name: '{Name}')");
        }
        builder.AppendLine();
    }
}
