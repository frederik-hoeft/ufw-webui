namespace Ufw.Client.Api;

public sealed class ApiProblemDetails
{
    public string? Title { get; init; }

    public string? Detail { get; init; }

    public string? Message { get; init; }

    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
}
