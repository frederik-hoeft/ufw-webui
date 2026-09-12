using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Shared.Tests.Firewall;

[TestClass]
public sealed class FirewallRuleSnapshotFingerprintTests
{
    [TestMethod]
    public void Compute_ForKnownSnapshot_MatchesStableVector()
    {
        RuleListResponse snapshot = CreateSnapshot();

        string fingerprint = FirewallRuleSnapshotFingerprint.Compute(snapshot);

        Assert.AreEqual("sha256:tIsY-JU3ullaW6eYDewo2BWIh-y7duyEScQdLHcBR3o", fingerprint);
    }

    [TestMethod]
    public void Compute_SameSnapshot_IsDeterministic()
    {
        RuleListResponse snapshot = CreateSnapshot();

        string first = FirewallRuleSnapshotFingerprint.Compute(snapshot);
        string second = FirewallRuleSnapshotFingerprint.Compute(snapshot.Active, snapshot.Rules);

        Assert.AreEqual(first, second);
        Assert.StartsWith(FirewallRuleSnapshotFingerprint.PREFIX, first);
    }

    [TestMethod]
    public void Compute_ReorderedRows_ChangesFingerprint()
    {
        RuleListResponse snapshot = CreateSnapshot();
        RuleListResponse reordered = new(snapshot.Active, snapshot.Rules.Reverse().ToArray());

        Assert.AreNotEqual(
            FirewallRuleSnapshotFingerprint.Compute(snapshot),
            FirewallRuleSnapshotFingerprint.Compute(reordered));
    }

    [TestMethod]
    public void Compute_ActiveStateChange_ChangesFingerprint()
    {
        RuleListResponse snapshot = CreateSnapshot();
        RuleListResponse inactive = new(false, snapshot.Rules);

        Assert.AreNotEqual(
            FirewallRuleSnapshotFingerprint.Compute(snapshot),
            FirewallRuleSnapshotFingerprint.Compute(inactive));
    }

    [TestMethod]
    public void Compute_ObservedAndStructuredChanges_ChangeFingerprint()
    {
        RuleListResponse baseline = CreateSnapshot();
        ListedFirewallRule original = baseline.Rules[0];
        FirewallRuleSpecification originalRule = original.Rule!;

        RuleListResponse rawChanged = ReplaceFirst(baseline, CloneListed(original, rawLine: original.RawLine + " "));
        RuleListResponse displayNumberChanged = ReplaceFirst(baseline, CloneListed(original, displayNumber: 99));
        RuleListResponse identityChanged = ReplaceFirst(baseline, CloneListed(original, ruleId: original.RuleId + "x"));
        RuleListResponse parsedChanged = ReplaceFirst(baseline, CloneListed(original, parsed: false));
        RuleListResponse[] structuredChanges =
        [
            ReplaceFirst(baseline, CloneListed(original, rule: CloneRule(originalRule, action: FirewallAction.Deny))),
            ReplaceFirst(baseline, CloneListed(original, rule: CloneRule(originalRule, addressFamily: FirewallAddressFamily.IPv6))),
            ReplaceFirst(baseline, CloneListed(original, rule: CloneRule(originalRule, direction: FirewallDirection.Out))),
            ReplaceFirst(baseline, CloneListed(original, rule: CloneRule(originalRule, protocol: FirewallProtocol.Udp))),
            ReplaceFirst(baseline, CloneListed(original, rule: CloneRule(originalRule, source: "192.168.0.0/16"))),
            ReplaceFirst(baseline, CloneListed(original, rule: CloneRule(originalRule, sourcePorts: "1024"))),
            ReplaceFirst(baseline, CloneListed(original, rule: CloneRule(originalRule, sourceInterface: "eno1"))),
            ReplaceFirst(baseline, CloneListed(original, rule: CloneRule(originalRule, destination: "10.0.0.1"))),
            ReplaceFirst(baseline, CloneListed(original, rule: CloneRule(originalRule, destinationPorts: "443"))),
            ReplaceFirst(baseline, CloneListed(original, rule: CloneRule(originalRule, destinationInterface: "eno2"))),
            ReplaceFirst(baseline, CloneListed(original, rule: CloneRule(originalRule, comment: "different"))),
        ];

        string fingerprint = FirewallRuleSnapshotFingerprint.Compute(baseline);
        Assert.AreNotEqual(fingerprint, FirewallRuleSnapshotFingerprint.Compute(rawChanged));
        Assert.AreNotEqual(fingerprint, FirewallRuleSnapshotFingerprint.Compute(displayNumberChanged));
        Assert.AreNotEqual(fingerprint, FirewallRuleSnapshotFingerprint.Compute(identityChanged));
        Assert.AreNotEqual(fingerprint, FirewallRuleSnapshotFingerprint.Compute(parsedChanged));
        foreach (RuleListResponse changed in structuredChanges)
        {
            Assert.AreNotEqual(fingerprint, FirewallRuleSnapshotFingerprint.Compute(changed));
        }
    }

    [TestMethod]
    public void Compute_NullRuleEntry_IsRejected()
    {
        ListedFirewallRule?[] rules = [null];

        Assert.Throws<ArgumentException>(() => FirewallRuleSnapshotFingerprint.Compute(true, rules!));
    }

    private static RuleListResponse CreateSnapshot()
    {
        FirewallRuleSpecification rule = new()
        {
            Action = FirewallAction.Allow,
            AddressFamily = FirewallAddressFamily.IPv4,
            Direction = FirewallDirection.In,
            Protocol = FirewallProtocol.Tcp,
            Source = "10.0.0.0/8",
            SourcePorts = null,
            SourceInterface = "eno0",
            Destination = "any",
            DestinationPorts = "22",
            DestinationInterface = null,
            Comment = "ssh",
        };
        ListedFirewallRule parsed = new()
        {
            RuleId = RuleIdentity.Compute(rule),
            DisplayNumber = 1,
            Parsed = true,
            RawLine = "[ 1] 22/tcp ALLOW IN 10.0.0.0/8 on eno0 # ssh",
            Rule = rule,
        };
        ListedFirewallRule opaque = new()
        {
            RuleId = null,
            DisplayNumber = 2,
            Parsed = false,
            RawLine = "[ 2] unsupported raw ufw syntax",
            Rule = null,
        };
        return new RuleListResponse(true, [parsed, opaque]);
    }

    private static RuleListResponse ReplaceFirst(RuleListResponse source, ListedFirewallRule replacement)
    {
        ListedFirewallRule[] rules = source.Rules.ToArray();
        rules[0] = replacement;
        return new RuleListResponse(source.Active, rules);
    }

    private static ListedFirewallRule CloneListed(
        ListedFirewallRule source,
        int? displayNumber = null,
        bool? parsed = null,
        string? rawLine = null,
        string? ruleId = null,
        FirewallRuleSpecification? rule = null) => new()
        {
            RuleId = ruleId ?? source.RuleId,
            DisplayNumber = displayNumber ?? source.DisplayNumber,
            Parsed = parsed ?? source.Parsed,
            RawLine = rawLine ?? source.RawLine,
            Rule = rule ?? source.Rule,
        };

    private static FirewallRuleSpecification CloneRule(
        FirewallRuleSpecification sourceRule,
        FirewallAction? action = null,
        FirewallAddressFamily? addressFamily = null,
        FirewallDirection? direction = null,
        FirewallProtocol? protocol = null,
        string? source = null,
        string? sourcePorts = null,
        string? sourceInterface = null,
        string? destination = null,
        string? destinationPorts = null,
        string? destinationInterface = null,
        string? comment = null) => new()
        {
            Action = action ?? sourceRule.Action,
            AddressFamily = addressFamily ?? sourceRule.AddressFamily,
            Direction = direction ?? sourceRule.Direction,
            Protocol = protocol ?? sourceRule.Protocol,
            Source = source ?? sourceRule.Source,
            SourcePorts = sourcePorts ?? sourceRule.SourcePorts,
            SourceInterface = sourceInterface ?? sourceRule.SourceInterface,
            Destination = destination ?? sourceRule.Destination,
            DestinationPorts = destinationPorts ?? sourceRule.DestinationPorts,
            DestinationInterface = destinationInterface ?? sourceRule.DestinationInterface,
            Comment = comment ?? sourceRule.Comment,
        };
}
