namespace Ufw.Shared.Firewall.SystemState;

/// <summary>Availability and completeness of optional managed-UFW attribution for one address family.</summary>
public enum FilterEnrichmentStatus
{
    /// <summary>All available configured translations were reconciled with the observed physical inventory.</summary>
    Complete,

    /// <summary>Some configured translations could not be confidently associated with live occurrences.</summary>
    Partial,

    /// <summary>One or more optional managed-rule observations could not be obtained or parsed.</summary>
    Unavailable,

    /// <summary>Attribution exceeded its bounded work budget; the physical inventory remains complete.</summary>
    ResourceLimitReached,
}
