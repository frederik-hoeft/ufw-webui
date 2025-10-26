using System.Diagnostics;

namespace Ufw.Roslyn.Controllers.Routing;

[DebuggerDisplay("{RouteSegment}")]
internal class RoutingNode(RoutingNode? parent, string routeSegment, IRoutingSegment? segment)
{
    private readonly List<RoutingNode> _children = [];

    public RoutingNode? Parent { get; } = parent;

    public string RouteSegment { get; } = routeSegment;

    public IRoutingSegment? Segment { get; } = segment;

    public RoutingNode GetOrAddChild(string routeSegment, IRoutingSegment? childSegment)
    {
        foreach (RoutingNode child in _children)
        {
            if (child.RouteSegment.Equals(routeSegment, StringComparison.Ordinal))
            {
                return child;
            }
        }
        RoutingNode newChild = new(this, routeSegment, childSegment);
        _children.Add(newChild);
        return newChild;
    }

    public IRoutingSegment? FindBestMatch(string route)
    {
        ArgumentNullException.ThrowIfNull(route, nameof(route));
        return FindBestMatch(route.AsSpan());
    }

    protected virtual IRoutingSegment? FindBestMatch(ReadOnlySpan<char> route)
    {
        IRoutingSegment? bestMatch = Segment;
        if (route.IsEmpty)
        {
            return bestMatch;
        }

        int seperatorIndex = route.IndexOf('/');
        ReadOnlySpan<char> input = seperatorIndex == -1 ? route : route[..seperatorIndex];
        int start = seperatorIndex + 1;
        ReadOnlySpan<char> remainingRoute = route[start..];
        foreach (RoutingNode child in _children)
        {
            if (WildcardUtil.Matches(input, child.RouteSegment))
            {
                IRoutingSegment? match = child.FindBestMatch(remainingRoute);
                if ((match?.Priority ?? int.MaxValue) <= (bestMatch?.Priority ?? int.MaxValue))
                {
                    bestMatch = match;
                }
            }
        }
        return bestMatch;
    }
}
