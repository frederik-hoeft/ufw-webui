namespace Ufw.Web.Client.Features.Rules.Metadata;

// The client-domain model intentionally shares its noun with the Api.RuleMetadata resource namespace.
#pragma warning disable CA1724 // Type names should not match namespaces.
public sealed record RuleMetadata(Guid Id, string? Notes, IReadOnlyList<RuleTag> Tags, RuleGroupMembership? Group = null);
#pragma warning restore CA1724
