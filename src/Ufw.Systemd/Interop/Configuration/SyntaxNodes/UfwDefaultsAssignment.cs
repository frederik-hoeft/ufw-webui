namespace Ufw.Systemd.Interop.Configuration.SyntaxNodes;

internal readonly record struct UfwDefaultsAssignment(string Key, string? Value, bool IsValid);
