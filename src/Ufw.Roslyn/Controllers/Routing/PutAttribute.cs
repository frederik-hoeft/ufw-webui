namespace Ufw.Roslyn.Controllers.Routing;

[AttributeUsage(AttributeTargets.Method)]
public sealed class PutAttribute(string? route = null) : RouteAttributeBase(route);