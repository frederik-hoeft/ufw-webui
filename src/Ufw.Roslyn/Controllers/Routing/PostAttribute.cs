namespace Ufw.Roslyn.Controllers.Routing;

[AttributeUsage(AttributeTargets.Method)]
public sealed class PostAttribute(string? route = null) : RouteAttributeBase(route);