using Ufw.Shared.Domain.Algebra;

namespace Ufw.Web.Client.Features.Rules.Filtering.Semantics;

/// <summary>
/// A filter-facing numeric port set backed by the shared interval algebra.
/// </summary>
internal sealed record PortFilterOperand(IntervalSet<ushort> Ports, string CanonicalValue)
{
    public bool Overlaps(PortFilterOperand other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Ports.Overlaps(other.Ports);
    }
}
