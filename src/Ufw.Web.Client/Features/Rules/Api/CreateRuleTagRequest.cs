namespace Ufw.Web.Client.Features.Rules.Api;

public sealed class CreateRuleTagRequest
{
    public string Name { get; init; } = string.Empty;

    public string Color { get; init; } = string.Empty;
}
