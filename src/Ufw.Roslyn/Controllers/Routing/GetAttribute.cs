namespace Ufw.Roslyn.Controllers.Routing;

[AttributeUsage(AttributeTargets.Method)]
public sealed class GetAttribute(string? route = null) : RouteAttributeBase(route);