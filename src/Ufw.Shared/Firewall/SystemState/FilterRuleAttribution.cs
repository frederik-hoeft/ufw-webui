namespace Ufw.Shared.Firewall.SystemState;

/// <summary>Optional managed-UFW association for one authoritative physical rule occurrence.</summary>
public sealed record FilterRuleAttribution(FilterRuleLocation Location, UfwUserRuleReference UserRule);
