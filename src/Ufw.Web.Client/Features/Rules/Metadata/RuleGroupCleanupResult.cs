namespace Ufw.Web.Client.Features.Rules.Metadata;

internal sealed record RuleGroupCleanupResult(bool Deleted, IReadOnlyList<RuleGroup> Groups);
