using Ufw.Web.Model.V1.Rules;

namespace Ufw.Web.Services.Rules;

internal sealed record RuleMetadataSaveResult(RuleMetadataSaveOutcome Outcome, RuleMetadataItem? Metadata = null);
