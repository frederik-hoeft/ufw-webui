using Ufw.Web.Api.V1.Models.Rules;

namespace Ufw.Web.Services.Rules;

internal sealed record RuleMetadataSaveResult(RuleMetadataSaveOutcome Outcome, RuleMetadataItem? Metadata = null);
