namespace Ufw.Roslyn.Controllers.Routing;

internal sealed class RoutingTree(string method) : RoutingNode(null, string.Empty, null)
{
    public string Method { get; } = method;

    protected override IRoutingSegment? FindBestMatch(ReadOnlySpan<char> route)
    {
        if (route.Length < 1 || route[0] != '/')
        {
            return null;
        }
        return base.FindBestMatch(route[1..]);
    }
}
