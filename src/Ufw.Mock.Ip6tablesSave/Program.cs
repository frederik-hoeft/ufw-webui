using Ufw.Mock.Iptables;
using Ufw.Shared.Firewall;

Environment.ExitCode = IptablesSaveApplication.Run(FirewallAddressFamily.IPv6, args);
