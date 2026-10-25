using Ufw.Shared.Firewall;

namespace Ufw.Systemd.Interop.Iptables.Parsing;

/// <summary>Validated shape of a UFW tuple annotation, independent of the mutable rule model.</summary>
internal sealed class UfwTupleSyntax
{
    public required FirewallAction Action { get; init; }
    public required bool Forward { get; init; }
    public required UfwTupleLogging Logging { get; init; }
    public required string Protocol { get; init; }
    public required string Source { get; init; }
    public required string Destination { get; init; }
    public required string SourcePorts { get; init; }
    public required string DestinationPorts { get; init; }
    public required string Direction { get; init; }
    public required string InterfaceIn { get; init; }
    public required string InterfaceOut { get; init; }
    public string? SourceApplication { get; init; }
    public string? DestinationApplication { get; init; }
    public string? Comment { get; init; }
}
