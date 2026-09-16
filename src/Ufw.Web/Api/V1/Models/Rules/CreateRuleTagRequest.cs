namespace Ufw.Web.Api.V1.Models.Rules;

public sealed class CreateRuleTagRequest
{
    public string Name { get; init; } = string.Empty;

    public string Color { get; init; } = string.Empty;
}
