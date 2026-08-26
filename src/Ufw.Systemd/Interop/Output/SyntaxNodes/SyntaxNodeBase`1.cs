using System.Text;

namespace Ufw.Systemd.Interop.Output.SyntaxNodes;

internal abstract class SyntaxNodeBase<TResult>(string? name, TResult result) : SyntaxNodeBase(name), ISyntaxNode<TResult>
{
    public TResult Evaluate() => result;

    public override void ToString(StringBuilder builder, int indentLevel)
    {
        builder.Append(' ', indentLevel * 2);
        builder.Append(GetType().Name);
        builder.Append($" (Name: '{Name ?? "<unnamed>"}', Result: '{result}')");
        builder.AppendLine();
    }
}
