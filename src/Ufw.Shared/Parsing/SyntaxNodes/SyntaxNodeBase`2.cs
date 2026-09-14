using System.Text;
using Ufw.Shared.Parsing.Visitors;

namespace Ufw.Shared.Parsing.SyntaxNodes;

public abstract class SyntaxNodeBase<TVisitor, TResult>(string? name, TResult result) : SyntaxNodeBase<TVisitor>(name), ISyntaxNode<TResult>
    where TVisitor : class, INodeVisitor
{
    public TResult Evaluate() => result;

    public override void ToString(StringBuilder builder, int indentLevel)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Append(' ', indentLevel * 2);
        builder.Append(GetType().Name);
        builder.Append($" (Name: '{Name ?? "<unnamed>"}', Result: '{result}')");
        builder.AppendLine();
    }
}
