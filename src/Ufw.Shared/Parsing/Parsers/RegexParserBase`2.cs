using Ufw.Shared.Parsing.Visitors;

namespace Ufw.Shared.Parsing.Parsers;

public abstract class RegexParserBase<TSelf, TVisitor>(string? name) : RegexParserBase<TSelf>(name)
    where TSelf : RegexParserBase<TSelf, TVisitor>, IRegexOwner
    where TVisitor : class, INodeVisitor
{
    public override bool CanAccept(Type visitorType)
    {
        ArgumentNullException.ThrowIfNull(visitorType);
        return typeof(TVisitor).IsAssignableFrom(visitorType);
    }
}
