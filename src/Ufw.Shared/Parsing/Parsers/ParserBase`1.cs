using Ufw.Shared.Parsing.Visitors;

namespace Ufw.Shared.Parsing.Parsers;

public abstract class ParserBase<TVisitor> : ParserBase
    where TVisitor : class, INodeVisitor
{
    public override bool CanAccept(Type visitorType)
    {
        ArgumentNullException.ThrowIfNull(visitorType);
        return typeof(TVisitor).IsAssignableFrom(visitorType);
    }
}
