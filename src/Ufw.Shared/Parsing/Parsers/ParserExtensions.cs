using Ufw.Shared.Parsing.Visitors;

namespace Ufw.Shared.Parsing.Parsers;

public static class ParserExtensions
{
    public static bool CanAccept<TVisitor>(this IParser parser)
        where TVisitor : class, INodeVisitor
    {
        ArgumentNullException.ThrowIfNull(parser);
        return parser.CanAccept(typeof(TVisitor));
    }

    public static IParser RequireVisitor<TVisitor>(this IParser parser)
        where TVisitor : class, INodeVisitor
    {
        ArgumentNullException.ThrowIfNull(parser);
        if (!parser.CanAccept<TVisitor>())
        {
            throw new InvalidOperationException($"Parser '{parser.Name ?? parser.GetType().Name}' does not support visitor '{typeof(TVisitor).FullName}'.");
        }
        return parser;
    }
}
