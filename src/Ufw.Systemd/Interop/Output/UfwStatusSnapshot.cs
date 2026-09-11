namespace Ufw.Systemd.Interop.Output;

internal sealed record UfwStatusSnapshot(bool Active, IReadOnlyList<ObservedUfwRule> Rules);
