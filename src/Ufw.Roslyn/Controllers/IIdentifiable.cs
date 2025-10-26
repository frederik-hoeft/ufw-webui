namespace Ufw.Roslyn.Controllers;

public interface IIdentifiable
{
    string Id { get; }

    string? Method { get; }
}
