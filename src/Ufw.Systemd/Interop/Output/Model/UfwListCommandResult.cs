namespace Ufw.Systemd.Interop.Output.Model;

internal sealed record UfwListCommandResult(bool Status, List<UfwListCommandResultRow> Rows);
