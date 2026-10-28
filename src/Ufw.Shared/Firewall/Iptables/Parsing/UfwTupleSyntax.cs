using Ufw.Shared.Firewall;

namespace Ufw.Shared.Firewall.Iptables.Parsing;

/// <summary>Decoded UFW tuple fields, independent of the mutable rule model.</summary>
/// <remarks>Syntax validity does not imply every field can be mapped to a supported firewall specification.</remarks>
public sealed class UfwTupleSyntax
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
