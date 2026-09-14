using System.Text;

namespace Ufw.Shared.Parsing.SyntaxNodes;

public abstract class ResultSyntaxNodeBase<TResult>(string? name, TResult result) : SyntaxNodeBase(name), ISyntaxNode<TResult>
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
