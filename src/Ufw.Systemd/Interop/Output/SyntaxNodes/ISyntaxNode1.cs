namespace Ufw.Systemd.Interop.Output.SyntaxNodes;

internal interface ISyntaxNode<out TResult> : ISyntaxNode
{
    TResult Evaluate();
}
