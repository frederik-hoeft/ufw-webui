using Ufw.Shared.Firewall;

namespace Ufw.Mock.Iptables;

/// <summary>
/// The stock UFW 0.36.2 before/after filter behavior, expressed as rules rather than
/// loading configuration files on every read. Custom fixtures can extend these chains.
/// </summary>
internal static class MockUfwDefaultRules
{
    public static void AddBefore(FirewallAddressFamily family, string prefix, Action<string> append)
    {
        ArgumentNullException.ThrowIfNull(append);
        string input = prefix + "before-input";
        string output = prefix + "before-output";
        string forward = prefix + "before-forward";

        append($"-A {input} -i lo -j ACCEPT");
        append($"-A {output} -o lo -j ACCEPT");

        if (family == FirewallAddressFamily.IPv6)
        {
            foreach (string chain in new[] { input, forward, output })
            {
                append($"-A {chain} -m rt --rt-type 0 -j DROP");
            }
        }

        foreach (string chain in new[] { input, output, forward })
        {
            append($"-A {chain} -m conntrack --ctstate RELATED,ESTABLISHED -j ACCEPT");
        }

        if (family == FirewallAddressFamily.IPv6)
        {
            append($"-A {input} -p icmpv6 --icmpv6-type echo-reply -j ACCEPT");
        }
        append($"-A {input} -m conntrack --ctstate INVALID -j {prefix}logging-deny");
        append($"-A {input} -m conntrack --ctstate INVALID -j DROP");

        if (family == FirewallAddressFamily.IPv4)
        {
            foreach (string chain in new[] { input, forward })
            {
                foreach (string type in new[] { "destination-unreachable", "time-exceeded", "parameter-problem", "echo-request" })
                {
                    append($"-A {chain} -p icmp --icmp-type {type} -j ACCEPT");
                }
            }
            append($"-A {input} -p udp --sport 67 --dport 68 -j ACCEPT");
            append($"-A {input} -j {prefix}not-local");
            foreach (string type in new[] { "LOCAL", "MULTICAST", "BROADCAST" })
            {
                append($"-A {prefix}not-local -m addrtype --dst-type {type} -j RETURN");
            }
            append($"-A {prefix}not-local -m limit --limit 3/min --limit-burst 10 -j {prefix}logging-deny");
            append($"-A {prefix}not-local -j DROP");
            append($"-A {input} -p udp -d 224.0.0.251 --dport 5353 -j ACCEPT");
            append($"-A {input} -p udp -d 239.255.255.250 --dport 1900 -j ACCEPT");
            return;
        }

        string[] commonTypes = ["destination-unreachable", "packet-too-big", "time-exceeded", "parameter-problem", "echo-request"];
        foreach (string chain in new[] { input, output, forward })
        {
            foreach (string type in commonTypes)
            {
                append($"-A {chain} -p icmpv6 --icmpv6-type {type} -j ACCEPT");
            }
        }
        append($"-A {output} -p icmpv6 --icmpv6-type echo-reply -j ACCEPT");
        append($"-A {forward} -p icmpv6 --icmpv6-type echo-reply -j ACCEPT");

        foreach (string chain in new[] { input, output })
        {
            foreach (string type in new[] { "router-solicitation", "router-advertisement", "neighbor-solicitation", "neighbor-advertisement", "141", "142" })
            {
                append($"-A {chain} -p icmpv6 --icmpv6-type {type} -m hl --hl-eq 255 -j ACCEPT");
            }
            foreach (string type in new[] { "130", "131", "132", "143" })
            {
                append($"-A {chain} -p icmpv6 --icmpv6-type {type} -s fe80::/10 -j ACCEPT");
            }
            foreach (string type in new[] { "148", "149" })
            {
                append($"-A {chain} -p icmpv6 --icmpv6-type {type} -m hl --hl-eq 255 -j ACCEPT");
            }
            foreach (string type in new[] { "151", "152", "153" })
            {
                append($"-A {chain} -p icmpv6 --icmpv6-type {type} -s fe80::/10 -m hl --hl-eq 1 -j ACCEPT");
            }
        }
        foreach (string type in new[] { "144", "145", "146", "147" })
        {
            append($"-A {input} -p icmpv6 --icmpv6-type {type} -j ACCEPT");
        }
        append($"-A {input} -p udp -s fe80::/10 --sport 547 -d fe80::/10 --dport 546 -j ACCEPT");
        append($"-A {input} -p udp -d ff02::fb --dport 5353 -j ACCEPT");
        append($"-A {input} -p udp -d ff02::f --dport 1900 -j ACCEPT");
    }

    public static void AddAfter(FirewallAddressFamily family, string prefix, Action<string> append)
    {
        ArgumentNullException.ThrowIfNull(append);
        string chain = prefix + "after-input";
        foreach ((string protocol, int port) in new (string, int)[]
        {
            ("udp", 137), ("udp", 138), ("tcp", 139), ("tcp", 445),
            ("udp", family == FirewallAddressFamily.IPv4 ? 67 : 546),
            ("udp", family == FirewallAddressFamily.IPv4 ? 68 : 547),
        })
        {
            append($"-A {chain} -p {protocol} --dport {port} -j {prefix}skip-to-policy-input");
        }
        if (family == FirewallAddressFamily.IPv4)
        {
            append($"-A {chain} -m addrtype --dst-type BROADCAST -j {prefix}skip-to-policy-input");
        }
    }
}
