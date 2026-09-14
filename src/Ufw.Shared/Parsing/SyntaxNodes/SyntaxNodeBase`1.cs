using Ufw.Shared.Parsing.Visitors;

namespace Ufw.Shared.Parsing.SyntaxNodes;

public abstract class SyntaxNodeBase<TVisitor>(string? name) : SyntaxNodeBase(name)
    where TVisitor : class, INodeVisitor
{
    public sealed override void Accept(INodeVisitor visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        if (visitor is not TVisitor typedVisitor)
        {
            throw new ArgumentException($"Syntax node '{GetType().FullName}' requires visitor '{typeof(TVisitor).FullName}', but received '{visitor.GetType().FullName}'.", nameof(visitor));
        }
        Accept(typedVisitor);
    }

    protected abstract void Accept(TVisitor visitor);
}
