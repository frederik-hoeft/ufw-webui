namespace Ufw.Roslyn.Controllers.Routing;

[AttributeUsage(AttributeTargets.Class)]
public sealed class RouteAttribute(string? route = null) : RouteAttributeBase(route);
