using Ufw.Shared.Firewall;

namespace Ufw.Web.Client.Features.Rules.Ordering;

/// <summary>
/// Browser-local ordering gesture. The occurrence ID is the zero-based row position in the authoritative preview baseline,
/// while <see cref="TargetFamilyPosition"/> is one-based within the rule's concrete UFW address-family partition.
/// </summary>
public sealed record RuleMoveRequest(int OccurrenceId, FirewallAddressFamily AddressFamily, int TargetFamilyPosition);
