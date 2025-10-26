namespace Ufw.Systemd.Interop.Output.Model;

internal sealed class UfwListCommandResultRow
{
    public int RowNumber { get; set; }

    public UfwRuleContext? Context { get; set; }

    public RuleType Type { get; set; }

    public Direction Direction { get; set; }

    public string? SourceInterface { get; set; }

    public string? DestinationInterface { get; set; }

    public string? Source { get; set; }

    public string? Destination { get; set; }

    public UfwProtocol Protocol { get; set; } = UfwProtocol.Any;

    public string? SourcePorts { get; set; }

    public string? DestinationPorts { get; set; }

    public string? Comment { get; set; }
}
