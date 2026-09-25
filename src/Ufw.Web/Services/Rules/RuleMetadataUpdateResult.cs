using Ufw.Web.Model.V1.Rules;

namespace Ufw.Web.Services.Rules;

public sealed record RuleMetadataUpdateResult(RuleMetadataUpdateOutcome Outcome, RuleMetadataMutationResponse? Response = null);
