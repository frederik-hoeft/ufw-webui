namespace Ufw.Roslyn.Controllers.Routing;

public abstract class RouteAttributeBase(string? route = null) : Attribute
{
    public string? Route { get; } = route;

    public int Priority { get; set; }
}
