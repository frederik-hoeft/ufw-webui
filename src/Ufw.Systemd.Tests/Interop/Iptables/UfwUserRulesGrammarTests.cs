using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.SystemState;
using Ufw.Systemd.Interop.Iptables.Grammars;

namespace Ufw.Systemd.Tests.Interop.Iptables;

[TestClass]
public sealed class UfwUserRulesGrammarTests
{
    [TestMethod]
    public void TryParse_MapsTuplesOntoSpecificationsAndMultipleIptablesLines()
    {
        const string rules = """
            *filter
            :ufw-user-input - [0:0]
            :ufw-user-output - [0:0]
            :ufw-user-forward - [0:0]
            :ufw-user-limit - [0:0]
            :ufw-user-limit-accept - [0:0]
            :ufw-user-logging-input - [0:0]
            ### RULES ###

            ### tuple ### allow tcp 22 0.0.0.0/0 any 0.0.0.0/0 in comment=68656c6c6f
            -A ufw-user-input -p tcp --dport 22 -j ACCEPT

            ### tuple ### limit tcp 22 0.0.0.0/0 any 10.1.2.3/24 in_eth0
            -A ufw-user-input -i eth0 -p tcp --dport 22 -s 10.1.2.3/24 -m conntrack --ctstate NEW -m recent --set
            -A ufw-user-input -i eth0 -p tcp --dport 22 -s 10.1.2.3/24 -m conntrack --ctstate NEW -m recent --update --seconds 30 --hitcount 6 -j ufw-user-limit
            -A ufw-user-input -i eth0 -p tcp --dport 22 -s 10.1.2.3/24 -j ufw-user-limit-accept

            ### tuple ### allow_log udp 53 0.0.0.0/0 any 0.0.0.0/0 out
            -A ufw-user-output -p udp --dport 53 -j ufw-user-logging-output
            -A ufw-user-logging-output -p udp --dport 53 -m conntrack --ctstate NEW -m limit --limit 3/min --limit-burst 10 -j LOG --log-prefix "[UFW ALLOW] "
            -A ufw-user-logging-output -p udp --dport 53 -j RETURN
            -A ufw-user-output -p udp --dport 53 -j ACCEPT

            ### tuple ### route:deny any any 192.168.0.0/16 any 10.0.0.0/8 in_eth0!out_eth1
            -A ufw-user-forward -i eth0 -o eth1 -p all -s 10.0.0.0/8 -d 192.168.0.0/16 -j DROP

            ### tuple ### allow tcp 80,443 0.0.0.0/0 any 0.0.0.0/0 Apache - in
            -A ufw-user-input -p tcp -m multiport --dports 80,443 -m comment --comment 'dapp_Apache' -j ACCEPT

            ### tuple ### allow esp any 0.0.0.0/0 any 0.0.0.0/0 in
            -A ufw-user-input -p esp -j ACCEPT

            ### END RULES ###
            ### LOGGING ###
            -A ufw-user-logging-input -m limit --limit 3/min --limit-burst 10 -j LOG --log-prefix "[UFW BLOCK] "
            ### END LOGGING ###
            ### RATE LIMITING ###
            -A ufw-user-limit -m limit --limit 3/minute -j LOG --log-prefix "[UFW LIMIT BLOCK] "
            -A ufw-user-limit -j REJECT
            -A ufw-user-limit-accept -j ACCEPT
            ### END RATE LIMITING ###
            COMMIT
            """;

        Assert.IsTrue(UfwUserRulesGrammar.Instance.TryParse(rules, FirewallAddressFamily.IPv4, out IReadOnlyList<UfwUserRuleTranslation>? translations));
        Assert.HasCount(6, translations);

        UfwUserRuleTranslation ssh = translations[0];
        Assert.AreEqual("hello", ssh.Specification!.Comment);
        Assert.AreEqual(FirewallAction.Allow, ssh.Specification.Action);
        Assert.AreEqual("22", ssh.Specification.DestinationPorts);
        Assert.AreEqual(RuleIdentity.Compute(ssh.Specification), ssh.RuleId);
        Assert.HasCount(1, ssh.IptablesRules);
        Assert.IsFalse(ssh.IptablesRules[0].Clauses.Any(static clause => clause.Kind == FilterClauseKind.Opaque));

        UfwUserRuleTranslation limit = translations[1];
        Assert.AreEqual(FirewallAction.Limit, limit.Specification!.Action);
        Assert.AreEqual("eth0", limit.Specification.DestinationInterface);
        Assert.AreEqual("10.1.2.0/24", limit.Specification.Source);
        Assert.HasCount(3, limit.IptablesRules);
        Assert.AreEqual(FilterTargetKind.None, limit.IptablesRules[0].Target.Kind);
        Assert.AreEqual("ufw-user-limit", limit.IptablesRules[1].Target.Name);
        Assert.AreEqual("ufw-user-limit-accept", limit.IptablesRules[2].Target.Name);

        UfwUserRuleTranslation logged = translations[2];
        Assert.AreEqual(FirewallAction.Allow, logged.Specification!.Action);
        Assert.AreEqual(FirewallDirection.Out, logged.Specification.Direction);
        Assert.AreEqual(FirewallProtocol.Udp, logged.Specification.Protocol);
        Assert.HasCount(4, logged.IptablesRules);
        Assert.AreEqual("ufw-user-logging-output", logged.IptablesRules[1].ChainName);
        StringAssert.Contains(logged.IptablesRules[1].Target.Arguments, "[UFW ALLOW]");

        UfwUserRuleTranslation route = translations[3];
        Assert.AreEqual(FirewallDirection.Forward, route.Specification!.Direction);
        Assert.AreEqual(FirewallAction.Deny, route.Specification.Action);
        Assert.AreEqual("eth0", route.Specification.SourceInterface);
        Assert.AreEqual("eth1", route.Specification.DestinationInterface);
        Assert.AreEqual("10.0.0.0/8", route.Specification.Source);
        Assert.AreEqual("192.168.0.0/16", route.Specification.Destination);
        Assert.IsFalse(route.IptablesRules[0].Clauses.Any(static clause => clause.Kind == FilterClauseKind.Protocol));

        UfwUserRuleTranslation app = translations[4];
        Assert.AreEqual("80,443", app.Specification!.DestinationPorts);
        Assert.AreEqual("-m comment --comment dapp_Apache", app.IptablesRules[0].Clauses.Single(static clause => clause.Kind == FilterClauseKind.Opaque).Value);

        UfwUserRuleTranslation esp = translations[5];
        Assert.IsNull(esp.Specification);
        Assert.IsNull(esp.RuleId);
        Assert.AreEqual("esp", esp.IptablesRules[0].Clauses.Single(static clause => clause.Kind == FilterClauseKind.Protocol).Value);
    }

    [TestMethod]
    public void TryParse_IgnoresBoilerplateInsideTheRulesSection()
    {
        const string rules = """
            *filter
            :ufw-user-input - [0:0]
            :ufw-user-limit - [0:0]
            :ufw-user-limit-accept - [0:0]
            ### RULES ###
            -A ufw-user-limit -m limit --limit 3/minute -j LOG --log-prefix "[UFW LIMIT BLOCK] "
            -A ufw-user-limit -j REJECT
            -A ufw-user-limit-accept -j ACCEPT
            COMMIT
            """;

        Assert.IsTrue(UfwUserRulesGrammar.Instance.TryParse(rules, FirewallAddressFamily.IPv4, out IReadOnlyList<UfwUserRuleTranslation>? translations));
        Assert.IsEmpty(translations);
    }

    [TestMethod]
    public void TryParse_ReadsLegacySixFieldTuplesAndIpv6Anywhere()
    {
        const string rules = """
            *filter
            :ufw6-user-input - [0:0]
            ### RULES ###
            ### tuple ### deny udp 53 ::/0 any ::/0
            -A ufw6-user-input -p udp --dport 53 -j DROP
            ### tuple ### allow any any ::/0 123 ::1 out_wg0
            -A ufw6-user-output -o wg0 -p udp --sport 123 -d ::1 -j ACCEPT
            COMMIT
            """;

        Assert.IsTrue(UfwUserRulesGrammar.Instance.TryParse(rules, FirewallAddressFamily.IPv6, out IReadOnlyList<UfwUserRuleTranslation>? translations));
        FirewallRuleSpecification inbound = translations[0].Specification!;
        Assert.AreEqual(FirewallDirection.In, inbound.Direction);
        Assert.AreEqual(FirewallProtocol.Udp, inbound.Protocol);
        Assert.AreEqual(RuleSpecificationNormalizer.ANY, inbound.Source);
        FirewallRuleSpecification outbound = translations[1].Specification!;
        Assert.AreEqual("wg0", outbound.SourceInterface);
        Assert.AreEqual("::1", outbound.Source);
        Assert.AreEqual(RuleSpecificationNormalizer.ANY, outbound.Destination);
        Assert.AreEqual("123", outbound.SourcePorts);
    }

    [TestMethod]
    public void TryParse_RejectsAMalformedTuple()
    {
        const string rules = """
            *filter
            :ufw-user-input - [0:0]
            ### RULES ###
            ### tuple ### allow tcp
            -A ufw-user-input -j ACCEPT
            ### END RULES ###
            COMMIT
            """;

        Assert.IsFalse(UfwUserRulesGrammar.Instance.TryParse(rules, FirewallAddressFamily.IPv4, out _));
    }

    [TestMethod]
    public void TryParse_ReadsInstalledUserRuleFilesWhenPresent()
    {
        ParseIfPresent("/etc/ufw/user.rules", FirewallAddressFamily.IPv4);
        ParseIfPresent("/etc/ufw/user6.rules", FirewallAddressFamily.IPv6);
        ParseIfPresent("/usr/share/ufw/user.rules", FirewallAddressFamily.IPv4);
    }

    private static void ParseIfPresent(string path, FirewallAddressFamily family)
    {
        if (!File.Exists(path))
        {
            return;
        }

        Assert.IsTrue(UfwUserRulesGrammar.Instance.TryParse(File.ReadAllText(path), family, out _), path);
    }
}
