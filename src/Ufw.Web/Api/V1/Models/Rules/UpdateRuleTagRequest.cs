namespace Ufw.Web.Api.V1.Models.Rules;

public sealed class UpdateRuleTagRequest
{
    public string Name { get; init; } = string.Empty;

    public string Color { get; init; } = string.Empty;
}
