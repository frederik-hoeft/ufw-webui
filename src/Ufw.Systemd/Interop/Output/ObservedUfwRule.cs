using Ufw.Systemd.Interop.Output.Model;

namespace Ufw.Systemd.Interop.Output;

internal sealed class ObservedUfwRule
{
    public required string RawLine { get; init; }

    public int DisplayNumber { get; init; }

    public UfwListCommandResultRow? Parsed { get; init; }
}
