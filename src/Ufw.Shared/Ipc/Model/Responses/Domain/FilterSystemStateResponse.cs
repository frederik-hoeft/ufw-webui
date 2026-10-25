using Ufw.Shared.Firewall.SystemState;

namespace Ufw.Shared.Ipc.Model.Responses.Domain;

/// <summary>
/// Read-only structural snapshot of the active IPv4 and IPv6 filter tables.
/// </summary>
public sealed record FilterSystemStateResponse(FilterSystemState State) : OkResponseBase;
