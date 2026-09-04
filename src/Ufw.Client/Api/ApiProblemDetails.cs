namespace Ufw.Client.Api;

public sealed class ApiProblemDetails
{
    public string? Title { get; set; }

    public string? Detail { get; set; }

    public string? Message { get; set; }

    public Dictionary<string, string[]>? Errors { get; set; }
}
