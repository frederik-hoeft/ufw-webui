using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.SystemState;
using Ufw.Systemd.Interop.Iptables.Grammars;
using Ufw.Systemd.Interop.Iptables.Parsing;

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
        Assert.DoesNotContain(static clause => clause.Kind == FilterClauseKind.Opaque, ssh.IptablesRules[0].Clauses);

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
        Assert.Contains("[UFW ALLOW]", logged.IptablesRules[1].Target.Arguments!);

        UfwUserRuleTranslation route = translations[3];
        Assert.AreEqual(FirewallDirection.Forward, route.Specification!.Direction);
        Assert.AreEqual(FirewallAction.Deny, route.Specification.Action);
        Assert.AreEqual("eth0", route.Specification.SourceInterface);
        Assert.AreEqual("eth1", route.Specification.DestinationInterface);
        Assert.AreEqual("10.0.0.0/8", route.Specification.Source);
        Assert.AreEqual("192.168.0.0/16", route.Specification.Destination);
        Assert.DoesNotContain(static clause => clause.Kind == FilterClauseKind.Protocol, route.IptablesRules[0].Clauses);

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
    public void TryParse_OnlyCollectsTranslationsWithinFilterTable()
    {
        const string rules = """
            *nat
            :POSTROUTING ACCEPT [0:0]
            ### RULES ###
            ### tuple ### allow tcp 22 0.0.0.0/0 any 0.0.0.0/0 in
            -A POSTROUTING -p tcp --dport 22 -j ACCEPT
            COMMIT
            *filter
            :ufw-user-input - [0:0]
            ### RULES ###
            ### tuple ### allow tcp 80 0.0.0.0/0 any 0.0.0.0/0 in
            -A ufw-user-input -p tcp --dport 80 -j ACCEPT
            COMMIT
            """;

        Assert.IsTrue(UfwUserRulesGrammar.Instance.TryParse(rules, FirewallAddressFamily.IPv4, out IReadOnlyList<UfwUserRuleTranslation>? translations));
        Assert.HasCount(1, translations);
        Assert.AreEqual("80", translations[0].Specification!.DestinationPorts);
        Assert.AreEqual("ufw-user-input", translations[0].IptablesRules.Single().ChainName);
    }

    [TestMethod]
    public void TryParse_RejectsInvalidTupleLayoutsAndOptions()
    {
        string[] invalidBodies =
        [
            "allow tcp 22 0.0.0.0/0 any 0.0.0.0/0 in extra",
            "allow tcp 22 0.0.0.0/0 any 0.0.0.0/0 in bogus invalid_direction",
            "route:bogus tcp 22 0.0.0.0/0 any 0.0.0.0/0 in",
            "allow_unknown tcp 22 0.0.0.0/0 any 0.0.0.0/0 in",
            "allow tcp 22 0.0.0.0/0 any 0.0.0.0/0 invalid_direction",
            "allow tcp 22 0.0.0.0/0 any 0.0.0.0/0 in_eth0!invalid",
            "allow tcp 22 0.0.0.0/0 any 0.0.0.0/0 in comment=xyz",
            "allow tcp 22 0.0.0.0/0 any 0.0.0.0/0 in comment=f",
        ];

        foreach (string body in invalidBodies)
        {
            string rules = $"*filter\n:ufw-user-input - [0:0]\n### RULES ###\n### tuple ### {body}\n-A ufw-user-input -j ACCEPT\nCOMMIT\n";
            Assert.IsFalse(UfwUserRulesGrammar.Instance.TryParse(rules, FirewallAddressFamily.IPv4, out _), body);
        }
    }

    [TestMethod]
    public void TryParse_HandlesEightFieldApplicationsAndRouteLogSuffixWithComments()
    {
        const string rules = """
            *filter
            :ufw-user-input - [0:0]
            :ufw-user-forward - [0:0]
            ### RULES ###
            ### tuple ### allow tcp 80,443 0.0.0.0/0 any 0.0.0.0/0 Apache -
            -A ufw-user-input -p tcp -m multiport --dports 80,443 -j ACCEPT
            ### tuple ### route:allow_log-all udp 53 0.0.0.0/0 any 0.0.0.0/0 "Apache Full" - in_eth0!out_eth1 comment=74657374
            -A ufw-user-forward -p udp --dport 53 -j ACCEPT
            COMMIT
            """;

        Assert.IsTrue(UfwUserRulesGrammar.Instance.TryParse(rules, FirewallAddressFamily.IPv4, out IReadOnlyList<UfwUserRuleTranslation>? translations));
        Assert.HasCount(2, translations);
        Assert.AreEqual("80,443", translations[0].Specification!.DestinationPorts);
        FirewallRuleSpecification first = translations[0].Specification!;
        FirewallRuleSpecification second = translations[1].Specification!;
        Assert.AreEqual(FirewallDirection.In, first.Direction);
        Assert.AreEqual(FirewallDirection.Forward, second.Direction);
        Assert.AreEqual("eth0", second.SourceInterface);
        Assert.AreEqual("eth1", second.DestinationInterface);
        Assert.AreEqual("test", second.Comment);
    }

    [TestMethod]
    public void TryParse_DecodesQuotedApplicationProfileEscapes()
    {
        const string quoted = """allow tcp 80 0.0.0.0/0 any 0.0.0.0/0 "Folder\\Path \"quoted\" \x" 'O\'Brien\\Profiles' in""";

        Assert.IsTrue(UfwTupleBodyGrammar.Instance.TryParse(quoted, out UfwTupleSyntax? tuple));
        Assert.AreEqual("Folder\\Path \"quoted\" x", tuple.DestinationApplication);
        Assert.AreEqual("O'Brien\\Profiles", tuple.SourceApplication);

        const string unquoted = """allow tcp 80 0.0.0.0/0 any 0.0.0.0/0 Bare\Name - in""";
        Assert.IsTrue(UfwTupleBodyGrammar.Instance.TryParse(unquoted, out UfwTupleSyntax? plain));
        Assert.AreEqual(@"Bare\Name", plain.DestinationApplication);
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
}
