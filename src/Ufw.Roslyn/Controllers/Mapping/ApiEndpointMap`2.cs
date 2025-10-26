using System.Collections.Frozen;
using Ufw.Roslyn.Controllers.Routing;

namespace Ufw.Roslyn.Controllers.Mapping;

public abstract class ApiEndpointMap<TRequestEnvelope, TResponseEnvelope> : IApiEndpointMap<TRequestEnvelope, TResponseEnvelope>
{
    private readonly Lock _lock = new();
    private volatile FrozenDictionary<string, RoutingTree>? _routingForest;

    protected abstract FrozenSet<string> SupportedMethods { get; }

    protected abstract ApiEndpointMapping<TRequestEnvelope, TResponseEnvelope>[] GetMappings();

    public abstract IApiEndpoint<TRequestEnvelope, TResponseEnvelope> GetNotFoundEndpoint();

    public abstract IApiEndpoint<TRequestEnvelope, TResponseEnvelope> GetUnsupportedMethodEndpoint();

    public IApiEndpoint<TRequestEnvelope, TResponseEnvelope> Match(string method, string route)
    {
        if (!GetRoutingForest().TryGetValue(method, out RoutingTree? routingTree))
        {
            return GetUnsupportedMethodEndpoint();
        }

        if (routingTree.FindBestMatch(route) is not IApiEndpoint<TRequestEnvelope, TResponseEnvelope> apiEndpoint)
        {
            apiEndpoint = GetNotFoundEndpoint();
        }

        return apiEndpoint ?? throw new InvalidOperationException($"No endpoint registration found to match '{method} {route}'");
    }

    private FrozenDictionary<string, RoutingTree> GetRoutingForest()
    {
        FrozenDictionary<string, RoutingTree>? routingForest = _routingForest;
        if (routingForest != null)
        {
            return routingForest;
        }

        lock (_lock)
        {
            routingForest = _routingForest;
            if (routingForest != null)
            {
                return routingForest;
            }

            routingForest = CreateRoutingForest(SupportedMethods, GetMappings());
            _routingForest = routingForest;
        }
        return routingForest;
    }

    private static FrozenDictionary<string, RoutingTree> CreateRoutingForest(FrozenSet<string> supportedMethods, ApiEndpointMapping<TRequestEnvelope, TResponseEnvelope>[] bindings)
    {
        FrozenDictionary<string, RoutingTree> frozenDictionary = supportedMethods
            .Select(static method => new RoutingTree(method))
            .ToDictionary(static tree => tree.Method, static tree => tree)
            .ToFrozenDictionary();

        foreach (ApiEndpointMapping<TRequestEnvelope, TResponseEnvelope> binding in bindings)
        {
            if (!frozenDictionary.TryGetValue(binding.Method, out RoutingTree? routingTree))
            {
                throw new InvalidOperationException($"Failed to construct routing tree for unrecognized method '{binding.Method}'");
            }

            ReadOnlySpan<string> segments = GetSegments(binding.Route);
            RoutingNode routingNode = routingTree;
            for (int index = 0; index < segments.Length; ++index)
            {
                string routeSegment = segments[index];
                routingNode = routingNode.GetOrAddChild(routeSegment, index == segments.Length - 1 ? binding : null);
            }
        }
        return frozenDictionary;
    }

    private static ReadOnlySpan<string> GetSegments(string route) => route.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
