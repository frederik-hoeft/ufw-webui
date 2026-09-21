namespace Ufw.Web.Client.Infrastructure.Http;

public sealed class ApiProblemDetails
{
    public string? Title { get; init; }

    public string? Detail { get; init; }

    public string? Message { get; init; }

    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
}
