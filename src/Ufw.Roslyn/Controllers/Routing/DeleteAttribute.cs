namespace Ufw.Roslyn.Controllers.Routing;

[AttributeUsage(AttributeTargets.Method)]
public sealed class DeleteAttribute(string? route = null) : RouteAttributeBase(route);
