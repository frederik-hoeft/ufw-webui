namespace Ufw.Web.Model.V1.Rules;

public sealed class RuleMetadataMutationResponse
{
    public RuleMetadataMutationResponse() { }

    public RuleMetadataMutationResponse(RuleMetadataItem? metadata) => Metadata = metadata;

    public RuleMetadataItem? Metadata { get; init; }
}
