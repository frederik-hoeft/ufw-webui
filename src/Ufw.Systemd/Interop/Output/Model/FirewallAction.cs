namespace Ufw.Systemd.Interop.Output.Model;

internal readonly record struct FirewallAction(RuleType RuleType, Direction Direction);