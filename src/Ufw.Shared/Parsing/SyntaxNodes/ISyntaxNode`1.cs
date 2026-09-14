namespace Ufw.Shared.Parsing.SyntaxNodes;

public interface ISyntaxNode<out TResult> : ISyntaxNode
{
    TResult Evaluate();
}
