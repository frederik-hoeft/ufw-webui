namespace Ufw.Shared.Firewall.SystemState;

/// <summary>Family-level status of optional managed-rule attribution, separate from physical inventory completeness.</summary>
public sealed record FilterEnrichmentReport(FilterEnrichmentStatus Status, string? Diagnostic = null);
