using System.Text.Json;
using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.SystemState;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Ipc.Serialization.Json;

namespace Ufw.Shared.Tests.Firewall.SystemState;

[TestClass]
public sealed class FilterSystemStateFactoryTests
{
    private static readonly string[] s_inputPath = ["INPUT"];
    private static readonly string[] s_beforeInputPath = ["INPUT", "ufw-before-input"];
    private static readonly string[] s_sharedInputPath = ["INPUT", "shared"];
    private static readonly int[] s_translationIndices = [0, 1, 2];

    [TestMethod]
    public void Create_InlinesJumpsAndRecordsProvenanceWithoutStoppingAtVerdicts()
    {
        RawFilterTable ipv4 = Table(
            Chain("INPUT", FilterChainPolicy.Drop,
                Rule("INPUT", 0, Jump("ufw-before-input"), Protocol("tcp"))),
            Chain("ufw-before-input", FilterChainPolicy.None,
                Rule("ufw-before-input", 0, Target("ACCEPT"), Interface(FilterClauseKind.IngressInterface, "lo")),
                Rule("ufw-before-input", 1, Jump("ufw-user-input")),
                Rule("ufw-before-input", 2, Target("DROP"), Opaque("-m conntrack --ctstate INVALID"))),
            Chain("ufw-user-input", FilterChainPolicy.None,
                Rule("ufw-user-input", 0, Target("ACCEPT"), Protocol("tcp"), Port(FilterClauseKind.DestinationPort, "22")),
                Rule("ufw-user-input", 1, Target("RETURN"))),
            Chain("OUTPUT", FilterChainPolicy.Accept),
            Chain("FORWARD", FilterChainPolicy.Drop));

        FilterSystemState state = FilterSystemStateFactory.Create(ipv4, EmptyV6());

        ResolvedFilterChain input = state.IPv4.Input;
        Assert.IsTrue(input.ChainPresent);
        Assert.AreEqual(FilterChainPolicy.Drop, input.Policy);
        Assert.HasCount(6, input.Rules);
        Assert.AreEqual("INPUT", input.Rules[0].ProvenanceChain);
        CollectionAssert.AreEqual(s_inputPath, input.Rules[0].ChainPath.ToArray());
        Assert.AreEqual("ufw-before-input", input.Rules[1].ProvenanceChain);
        CollectionAssert.AreEqual(s_beforeInputPath, input.Rules[1].ChainPath.ToArray());
        Assert.AreEqual("ufw-user-input", input.Rules[3].ProvenanceChain);
        Assert.AreEqual(0, input.Rules[3].Location.Index);
        Assert.AreEqual("RETURN", input.Rules[4].Target.Name);
        Assert.AreEqual("DROP", input.Rules[5].Target.Name);
        Assert.AreEqual("ufw-before-input", input.Rules[5].ProvenanceChain);
        Assert.IsTrue(input.Rules[0].Target.IsChainReference);
        Assert.IsFalse(input.Rules[1].Target.IsChainReference);
        Assert.IsEmpty(state.IPv4.Output.Rules);
        Assert.AreEqual(FilterChainPolicy.Accept, state.IPv4.Output.Policy);
    }

    [TestMethod]
    public void Create_ReentersAChainFromSeparateJumpsAndStopsOnACycle()
    {
        RawFilterTable ipv4 = Table(
            Chain("INPUT", FilterChainPolicy.Accept,
                Rule("INPUT", 0, Jump("shared")),
                Rule("INPUT", 1, Jump("shared"))),
            Chain("shared", FilterChainPolicy.None,
                Rule("shared", 0, Jump("shared"), Protocol("udp")),
                Rule("shared", 1, Target("ACCEPT"))),
            Chain("OUTPUT", FilterChainPolicy.Accept),
            Chain("FORWARD", FilterChainPolicy.Accept));

        ResolvedFilterChain input = FilterSystemStateFactory.Create(ipv4, EmptyV6()).IPv4.Input;

        Assert.HasCount(6, input.Rules);
        Assert.AreEqual(FilterTraversalStatus.CycleDetected, input.TraversalStatus);
        Assert.AreEqual(0, input.Rules[1].Location.Index);
        Assert.AreEqual(1, input.Rules[2].Location.Index);
        Assert.AreEqual(0, input.Rules[4].Location.Index);
        Assert.AreEqual(input.Rules[1].Location, input.Rules[4].Location);
        CollectionAssert.AreEqual(s_sharedInputPath, input.Rules[1].ChainPath.ToArray());
        CollectionAssert.AreEqual(s_sharedInputPath, input.Rules[4].ChainPath.ToArray());
    }

    [TestMethod]
    public void Create_DistinguishesRepeatedJumpEdgesWithoutDuplicatingPhysicalIdentities()
    {
        RawFilterTable table = Table(
            Chain("INPUT", FilterChainPolicy.Accept, Rule("INPUT", 0, Jump("shared")), Rule("INPUT", 1, Jump("shared"))),
            Chain("shared", FilterChainPolicy.None, Rule("shared", 0, Target("ACCEPT"))),
            Chain("OUTPUT", FilterChainPolicy.Accept), Chain("FORWARD", FilterChainPolicy.Accept));

        ResolvedFilterChain input = FilterSystemStateFactory.Create(table, EmptyV6()).IPv4.Input;

        Assert.AreEqual(FilterTraversalStatus.None, input.TraversalStatus);
        Assert.HasCount(4, input.Rules);
        Assert.AreEqual(input.Rules[1].Location, input.Rules[3].Location);
        CollectionAssert.AreEqual(new[] { new FilterRuleLocation(FirewallAddressFamily.IPv4, "INPUT", 0) }, input.Rules[1].JumpPath.ToArray());
        CollectionAssert.AreEqual(new[] { new FilterRuleLocation(FirewallAddressFamily.IPv4, "INPUT", 1) }, input.Rules[3].JumpPath.ToArray());
    }

    [TestMethod]
    public void Create_BoundsExponentiallyBranchingAcyclicGraphWithoutLosingRawRules()
    {
        const int depth = 24;
        List<RawFilterChain> chains = [];
        for (int index = 0; index < depth; index++)
        {
            string name = index == 0 ? "INPUT" : $"chain-{index}";
            string next = $"chain-{index + 1}";
            chains.Add(Chain(name, index == 0 ? FilterChainPolicy.Accept : FilterChainPolicy.None,
                Rule(name, 0, Jump(next)), Rule(name, 1, Jump(next))));
        }

        chains.Add(Chain($"chain-{depth}", FilterChainPolicy.None, Rule($"chain-{depth}", 0, Target("ACCEPT"))));
        chains.Add(Chain("OUTPUT", FilterChainPolicy.Accept));
        chains.Add(Chain("FORWARD", FilterChainPolicy.Accept));

        AddressFamilyFilterState state = FilterSystemStateFactory.Create(Table([.. chains]), EmptyV6()).IPv4;

        Assert.AreEqual(FilterTraversalStatus.RuleLimitReached, state.Input.TraversalStatus);
        Assert.HasCount(20_000, state.Input.Rules);
        Assert.AreEqual(depth + 3, state.Raw.Chains.Count);
        Assert.HasCount(1, state.Raw.Chains[depth].Rules);
        Assert.AreEqual(FilterTraversalStatus.None, state.Output.TraversalStatus);
    }

    [TestMethod]
    public void Create_LimitsDeepTraversalWithoutStackOverflow()
    {
        const int depth = 150;
        List<RawFilterChain> chains = [];
        for (int index = 0; index < depth; index++)
        {
            string name = index == 0 ? "INPUT" : $"chain-{index}";
            chains.Add(Chain(name, index == 0 ? FilterChainPolicy.Drop : FilterChainPolicy.None,
                Rule(name, 0, Jump($"chain-{index + 1}"))));
        }

        chains.Add(Chain($"chain-{depth}", FilterChainPolicy.None, Rule($"chain-{depth}", 0, Target("DROP"))));
        chains.Add(Chain("OUTPUT", FilterChainPolicy.Accept));
        chains.Add(Chain("FORWARD", FilterChainPolicy.Accept));

        AddressFamilyFilterState state = FilterSystemStateFactory.Create(Table([.. chains]), EmptyV6()).IPv4;

        Assert.AreEqual(FilterTraversalStatus.DepthLimitReached, state.Input.TraversalStatus);
        Assert.HasCount(128, state.Input.Rules);
        Assert.AreEqual(depth + 3, state.Raw.Chains.Count);
    }

    [TestMethod]
    public void Create_MissingBuiltInChainIsAnEmptyRoot()
    {
        RawFilterTable ipv4 = Table(Chain("OUTPUT", FilterChainPolicy.Accept), Chain("FORWARD", FilterChainPolicy.Drop));

        ResolvedFilterChain input = FilterSystemStateFactory.Create(ipv4, EmptyV6()).IPv4.Input;

        Assert.IsFalse(input.ChainPresent);
        Assert.AreEqual(FilterChainPolicy.None, input.Policy);
        Assert.IsEmpty(input.Rules);
    }

    [TestMethod]
    public void Create_AssociatesTranslationsInOrderAndKeepsUnmatchedRules()
    {
        FirewallRuleSpecification ssh = Spec(FirewallAction.Allow, "22");
        FirewallRuleSpecification duplicate = Spec(FirewallAction.Allow, "22");
        UfwUserRuleTranslation first = Translation(0, ssh, Translated("ufw-user-input", Target("ACCEPT"), Protocol("tcp"), Port(FilterClauseKind.DestinationPort, "22")));
        UfwUserRuleTranslation second = Translation(1, duplicate, Translated("ufw-user-input", Target("ACCEPT"), Protocol("tcp"), Port(FilterClauseKind.DestinationPort, "22")));
        UfwUserRuleTranslation absent = Translation(2, Spec(FirewallAction.Deny, "23"), Translated("ufw-user-input", Target("DROP"), Protocol("tcp"), Port(FilterClauseKind.DestinationPort, "23")));
        RawFilterTable ipv4 = Table(
            Chain("INPUT", FilterChainPolicy.Drop, Rule("INPUT", 0, Jump("ufw-user-input"))),
            Chain("ufw-user-input", FilterChainPolicy.None,
                Rule("ufw-user-input", 0, Target("DROP"), Opaque("-m conntrack --ctstate INVALID")),
                Rule("ufw-user-input", 1, Target("ACCEPT"), Port(FilterClauseKind.DestinationPort, "22"), Protocol("tcp")),
                Rule("ufw-user-input", 2, Target("ACCEPT"), Protocol("tcp"), Port(FilterClauseKind.DestinationPort, "22")),
                Rule("ufw-user-input", 3, Target("ACCEPT"), Protocol("udp"))),
            Chain("OUTPUT", FilterChainPolicy.Accept),
            Chain("FORWARD", FilterChainPolicy.Drop));

        FilterSystemState state = FilterSystemStateFactory.Create(ipv4, EmptyV6(), [first, second, absent]);
        ResolvedFilterRule[] userChain = [.. state.IPv4.Input.Rules.Where(static rule => rule.ProvenanceChain == "ufw-user-input")];

        Assert.IsNull(userChain[0].UserRule);
        UfwUserRuleReference firstLink = userChain[1].UserRule!;
        Assert.AreEqual(0, firstLink.UserRuleIndex);
        Assert.AreEqual(RuleIdentity.Compute(ssh), firstLink.RuleId);
        Assert.AreEqual(0, firstLink.TranslationIndex);
        Assert.AreEqual(1, firstLink.TranslationCount);
        Assert.AreEqual(1, userChain[2].UserRule!.UserRuleIndex);
        Assert.IsNull(userChain[3].UserRule);
        Assert.IsTrue(userChain.All(static rule => rule.UserRule?.UserRuleIndex != 2));
    }

    [TestMethod]
    public void Create_ReconcilesLaterManagedRulesWhenEarlierConfiguredRulesAreNotActive()
    {
        UfwUserRuleTranslation removed = Translation(0, Spec(FirewallAction.Allow, "22"), Translated("ufw-user-input", Target("ACCEPT"), Port(FilterClauseKind.DestinationPort, "22")));
        UfwUserRuleTranslation active = Translation(1, Spec(FirewallAction.Allow, "80"), Translated("ufw-user-input", Target("ACCEPT"), Port(FilterClauseKind.DestinationPort, "80")));
        RawFilterTable table = Table(
            Chain("INPUT", FilterChainPolicy.Drop, Rule("INPUT", 0, Jump("ufw-user-input"))),
            Chain("ufw-user-input", FilterChainPolicy.None, Rule("ufw-user-input", 0, Target("ACCEPT"), Port(FilterClauseKind.DestinationPort, "80"))),
            Chain("OUTPUT", FilterChainPolicy.Accept), Chain("FORWARD", FilterChainPolicy.Drop));

        UfwUserRuleReference? user = FilterSystemStateFactory.Create(table, EmptyV6(), [removed, active]).IPv4.Input.Rules[1].UserRule;

        Assert.IsNotNull(user);
        Assert.AreEqual(1, user.UserRuleIndex);
    }

    [TestMethod]
    public void Create_DoesNotGuessWhichIdenticalConfiguredOccurrenceIsInstalled()
    {
        UfwUserRuleTranslation first = Translation(0, Spec(FirewallAction.Allow, "22"), Translated("ufw-user-input", Target("ACCEPT"), Port(FilterClauseKind.DestinationPort, "22")));
        UfwUserRuleTranslation second = Translation(1, Spec(FirewallAction.Allow, "22"), Translated("ufw-user-input", Target("ACCEPT"), Port(FilterClauseKind.DestinationPort, "22")));
        RawFilterTable table = Table(
            Chain("INPUT", FilterChainPolicy.Drop, Rule("INPUT", 0, Jump("ufw-user-input"))),
            Chain("ufw-user-input", FilterChainPolicy.None, Rule("ufw-user-input", 0, Target("ACCEPT"), Port(FilterClauseKind.DestinationPort, "22"))),
            Chain("OUTPUT", FilterChainPolicy.Accept), Chain("FORWARD", FilterChainPolicy.Drop));

        Assert.IsNull(FilterSystemStateFactory.Create(table, EmptyV6(), [first, second]).IPv4.Input.Rules[1].UserRule);
    }

    [TestMethod]
    public void Create_DoesNotLinkIndividuallyUniqueButNonMandatoryAlignedRules()
    {
        UfwUserRuleTranslation first = Translation(0, Spec(FirewallAction.Allow, "22"), Translated("ufw-user-input", Target("ACCEPT"), Port(FilterClauseKind.DestinationPort, "22")));
        UfwUserRuleTranslation second = Translation(1, Spec(FirewallAction.Allow, "80"), Translated("ufw-user-input", Target("ACCEPT"), Port(FilterClauseKind.DestinationPort, "80")));
        RawFilterTable table = Table(
            Chain("INPUT", FilterChainPolicy.Drop, Rule("INPUT", 0, Jump("ufw-user-input"))),
            Chain("ufw-user-input", FilterChainPolicy.None,
                Rule("ufw-user-input", 0, Target("ACCEPT"), Port(FilterClauseKind.DestinationPort, "80")),
                Rule("ufw-user-input", 1, Target("ACCEPT"), Port(FilterClauseKind.DestinationPort, "22")),
                Rule("ufw-user-input", 2, Target("ACCEPT"), Port(FilterClauseKind.DestinationPort, "22"))),
            Chain("OUTPUT", FilterChainPolicy.Accept), Chain("FORWARD", FilterChainPolicy.Drop));

        ResolvedFilterRule[] userRules = [.. FilterSystemStateFactory.Create(table, EmptyV6(), [first, second]).IPv4.Input.Rules
            .Where(static rule => rule.ProvenanceChain == "ufw-user-input")];

        Assert.IsTrue(userRules.All(static rule => rule.UserRule is null));
    }

    [TestMethod]
    public void Create_DoesNotLinkAnOpaqueExpressionWithDifferentPredicates()
    {
        UfwUserRuleTranslation expected = Translation(0, Spec(FirewallAction.Allow, "22"),
            Translated("ufw-user-input", Target("ACCEPT"), Opaque("-m conntrack --ctstate NEW")));
        RawFilterTable table = Table(
            Chain("INPUT", FilterChainPolicy.Drop, Rule("INPUT", 0, Jump("ufw-user-input"))),
            Chain("ufw-user-input", FilterChainPolicy.None,
                Rule("ufw-user-input", 0, Target("ACCEPT"), Opaque("-m conntrack --ctstate NEW --ctstate INVALID"))),
            Chain("OUTPUT", FilterChainPolicy.Accept), Chain("FORWARD", FilterChainPolicy.Drop));

        Assert.IsNull(FilterSystemStateFactory.Create(table, EmptyV6(), [expected]).IPv4.Input.Rules[1].UserRule);
    }

    [TestMethod]
    public void Create_DoesNotAttributeConflictingAlternativeAlignments()
    {
        UfwUserRuleTranslation first = Translation(0, Spec(FirewallAction.Allow, "22"), Translated("ufw-user-input", Target("ACCEPT"), Port(FilterClauseKind.DestinationPort, "22")));
        UfwUserRuleTranslation second = Translation(1, Spec(FirewallAction.Allow, "80"), Translated("ufw-user-input", Target("ACCEPT"), Port(FilterClauseKind.DestinationPort, "80")));
        RawFilterTable table = Table(
            Chain("INPUT", FilterChainPolicy.Drop, Rule("INPUT", 0, Jump("ufw-user-input"))),
            Chain("ufw-user-input", FilterChainPolicy.None,
                Rule("ufw-user-input", 0, Target("ACCEPT"), Port(FilterClauseKind.DestinationPort, "80")),
                Rule("ufw-user-input", 1, Target("ACCEPT"), Port(FilterClauseKind.DestinationPort, "22"))),
            Chain("OUTPUT", FilterChainPolicy.Accept), Chain("FORWARD", FilterChainPolicy.Drop));

        ResolvedFilterRule[] user = [.. FilterSystemStateFactory.Create(table, EmptyV6(), [first, second]).IPv4.Input.Rules
            .Where(static rule => rule.ProvenanceChain == "ufw-user-input")];

        Assert.IsTrue(user.All(static rule => rule.UserRule is null));
    }

    [TestMethod]
    public void Create_OneUserRuleCoversManyIptablesLinesAndSaveNormalization()
    {
        FirewallRuleSpecification limit = Spec(FirewallAction.Limit, "22");
        UfwUserRuleTranslation translation = Translation(
            0,
            limit,
            Translated("ufw-user-input", NoneTarget(), Protocol("tcp"), Port(FilterClauseKind.DestinationPort, "22"), Opaque("-m conntrack --ctstate NEW"), Opaque("-m recent --set")),
            Translated("ufw-user-input", Jump("ufw-user-limit"), Protocol("tcp"), Port(FilterClauseKind.DestinationPort, "22"), Opaque("-m conntrack --ctstate NEW"), Opaque("-m recent --update --seconds 30 --hitcount 6")),
            Translated("ufw-user-input", Jump("ufw-user-limit-accept"), Protocol("tcp"), Port(FilterClauseKind.DestinationPort, "22")));
        RawFilterTable ipv4 = Table(
            Chain("INPUT", FilterChainPolicy.Drop, Rule("INPUT", 0, Jump("ufw-user-input"))),
            Chain("ufw-user-input", FilterChainPolicy.None,
                Rule("ufw-user-input", 0, NoneTarget(), Protocol("tcp"), Port(FilterClauseKind.DestinationPort, "22"), Opaque("-m conntrack --ctstate NEW"), Opaque("-m recent --set --name DEFAULT --mask 255.255.255.255 --rsource")),
                Rule("ufw-user-input", 1, Jump("ufw-user-limit"), Protocol("tcp"), Port(FilterClauseKind.DestinationPort, "22"), Opaque("-m conntrack --ctstate NEW"), Opaque("-m recent --update --seconds 30 --hitcount 6 --name DEFAULT --rsource")),
                Rule("ufw-user-input", 2, Jump("ufw-user-limit-accept"), Protocol("tcp"), Port(FilterClauseKind.DestinationPort, "22"))),
            Chain("ufw-user-limit", FilterChainPolicy.None, Rule("ufw-user-limit", 0, Target("REJECT", "--reject-with icmp-port-unreachable"))),
            Chain("ufw-user-limit-accept", FilterChainPolicy.None, Rule("ufw-user-limit-accept", 0, Target("ACCEPT"))),
            Chain("OUTPUT", FilterChainPolicy.Accept),
            Chain("FORWARD", FilterChainPolicy.Drop));

        ResolvedFilterRule[] input = [.. FilterSystemStateFactory.Create(ipv4, EmptyV6(), [translation]).IPv4.Input.Rules];
        ResolvedFilterRule[] linked = [.. input.Where(static rule => rule.UserRule is not null)];

        Assert.HasCount(3, linked);
        string limitId = RuleIdentity.Compute(limit);
        Assert.IsTrue(linked.All(rule => rule.UserRule!.TranslationCount == 3 && rule.UserRule.RuleId == limitId));
        CollectionAssert.AreEqual(s_translationIndices, linked.Select(static rule => rule.UserRule!.TranslationIndex).ToArray());
        Assert.IsTrue(input.Any(static rule => rule.ProvenanceChain == "ufw-user-limit" && rule.UserRule is null));
    }

    [TestMethod]
    public void Create_InlinesGotoAndStillEmitsTheCallingChainsLaterRules()
    {
        RawFilterTable ipv4 = Table(
            Chain("INPUT", FilterChainPolicy.Accept,
                Rule("INPUT", 0, new FilterTarget { Kind = FilterTargetKind.Goto, Name = "next", IsChainReference = true }),
                Rule("INPUT", 1, Target("ACCEPT"), Protocol("udp"))),
            Chain("next", FilterChainPolicy.None, Rule("next", 0, Target("DROP"))),
            Chain("OUTPUT", FilterChainPolicy.Accept),
            Chain("FORWARD", FilterChainPolicy.Accept));

        ResolvedFilterRule[] rules = [.. FilterSystemStateFactory.Create(ipv4, EmptyV6()).IPv4.Input.Rules];

        Assert.HasCount(3, rules);
        Assert.AreEqual(FilterTargetKind.Goto, rules[0].Target.Kind);
        Assert.AreEqual("next", rules[1].ProvenanceChain);
        Assert.AreEqual("udp", rules[2].Clauses[0].Value);
    }

    [TestMethod]
    public void Create_TreatsDefaultRejectArgumentsAsTheSameTranslation()
    {
        UfwUserRuleTranslation translation = Translation(
            0,
            Spec(FirewallAction.Reject, "22"),
            Translated("INPUT", Target("REJECT"), Protocol("tcp"), Port(FilterClauseKind.DestinationPort, "22")));
        RawFilterTable ipv4 = Table(
            Chain("INPUT", FilterChainPolicy.Drop, Rule("INPUT", 0, Target("REJECT", "--reject-with icmp-port-unreachable"), Protocol("tcp"), Port(FilterClauseKind.DestinationPort, "22"))),
            Chain("OUTPUT", FilterChainPolicy.Accept),
            Chain("FORWARD", FilterChainPolicy.Drop));

        UfwUserRuleReference? link = FilterSystemStateFactory.Create(ipv4, EmptyV6(), [translation]).IPv4.Input.Rules[0].UserRule;

        Assert.IsNotNull(link);
        Assert.AreEqual(0, link.UserRuleIndex);
        Assert.AreEqual(FirewallAction.Reject, link.Specification!.Action);
    }

    [TestMethod]
    public void Create_RetainsManagedAttributionsOutsideRootTraversal()
    {
        UfwUserRuleTranslation configured = Translation(0, Spec(FirewallAction.Allow, "80"),
            Translated("orphan", Target("ACCEPT"), Port(FilterClauseKind.DestinationPort, "80")));
        RawFilterTable table = Table(Chain("INPUT", FilterChainPolicy.Accept),
            Chain("orphan", FilterChainPolicy.None, Rule("orphan", 0, Target("ACCEPT"), Port(FilterClauseKind.DestinationPort, "80"))),
            Chain("OUTPUT", FilterChainPolicy.Accept), Chain("FORWARD", FilterChainPolicy.Drop));

        AddressFamilyFilterState state = FilterSystemStateFactory.Create(table, EmptyV6(), [configured]).IPv4;

        Assert.IsEmpty(state.Input.Rules);
        Assert.HasCount(1, state.ManagedRuleAttributions);
        Assert.AreEqual("orphan", state.ManagedRuleAttributions[0].Location.ChainName);
        Assert.AreEqual(0, state.ManagedRuleAttributions[0].UserRule.UserRuleIndex);
    }

    [TestMethod]
    public void Create_ReportsPartiallyMatchedTranslations()
    {
        UfwUserRuleTranslation configured = Translation(0, Spec(FirewallAction.Allow, "80"),
            Translated("ufw-user-input", Target("ACCEPT"), Port(FilterClauseKind.DestinationPort, "80")));
        RawFilterTable table = Table(Chain("INPUT", FilterChainPolicy.Accept),
            Chain("ufw-user-input", FilterChainPolicy.None), Chain("OUTPUT", FilterChainPolicy.Accept), Chain("FORWARD", FilterChainPolicy.Drop));

        AddressFamilyFilterState state = FilterSystemStateFactory.Create(table, EmptyV6(), [configured]).IPv4;

        Assert.AreEqual(FilterEnrichmentStatus.Partial, state.Enrichment.Status);
        Assert.HasCount(4, state.Raw.Chains);
    }

    [TestMethod]
    public void Create_ReportsSkippedResourceLimitedAlignment()
    {
        const int count = 2_100;
        UfwUserRuleTranslation configured = Translation(0, Spec(FirewallAction.Allow, "80"),
            Translated("ufw-user-input", Target("ACCEPT"), Port(FilterClauseKind.DestinationPort, "80")));
        RawFilterRule[] physical = new RawFilterRule[count];
        for (int index = 0; index < count; index++)
        {
            physical[index] = Rule("ufw-user-input", index, Target("ACCEPT"), Port(FilterClauseKind.DestinationPort, "80"));
        }

        UfwUserRuleTranslation[] translations = new UfwUserRuleTranslation[count];
        Array.Fill(translations, configured);
        RawFilterTable table = Table(Chain("INPUT", FilterChainPolicy.Accept),
            Chain("ufw-user-input", FilterChainPolicy.None, physical),
            Chain("OUTPUT", FilterChainPolicy.Accept), Chain("FORWARD", FilterChainPolicy.Drop));

        AddressFamilyFilterState state = FilterSystemStateFactory.Create(table, EmptyV6(), translations).IPv4;

        Assert.AreEqual(FilterEnrichmentStatus.ResourceLimitReached, state.Enrichment.Status);
        Assert.HasCount(count, state.Raw.Chains[1].Rules);
    }

    [TestMethod]
    public void Create_RejectsATableOfTheWrongFamily()
    {
        Assert.ThrowsExactly<ArgumentException>(() => FilterSystemStateFactory.Create(EmptyV6(), EmptyV6()));
    }

    [TestMethod]
    public void Response_RoundTripsThroughTheIpcSerializer()
    {
        FilterSystemState state = FilterSystemStateFactory.Create(
            Table(
                Chain("INPUT", FilterChainPolicy.Drop, Rule("INPUT", 0, Jump("custom"), Protocol("tcp"), Port(FilterClauseKind.DestinationPort, "80", negated: true))),
                Chain("custom", FilterChainPolicy.None, Rule("custom", 0, Target("ACCEPT"), Address(FilterClauseKind.SourceAddress, "10.0.0.0/8"))),
                Chain("OUTPUT", FilterChainPolicy.Accept),
                Chain("FORWARD", FilterChainPolicy.Drop)),
            EmptyV6(),
            [Translation(0, Spec(FirewallAction.Allow, "80"), Translated("custom", Target("ACCEPT"), Address(FilterClauseKind.SourceAddress, "10.0.0.0/8")))]);
        FilterSystemStateResponse response = new(state);

        string json = JsonSerializer.Serialize(response, MessageJsonSerializerContext.Default.FilterSystemStateResponse);
        FilterSystemStateResponse restored = JsonSerializer.Deserialize(json, MessageJsonSerializerContext.Default.FilterSystemStateResponse)!;

        Assert.AreEqual(FilterChainPolicy.Drop, restored.State.IPv4.Raw.Chains[0].Policy);
        ResolvedFilterRule enriched = restored.State.IPv4.Input.Rules[1];
        Assert.AreEqual("10.0.0.0/8", enriched.Clauses[0].Value);
        Assert.IsTrue(restored.State.IPv4.Input.Rules[0].Clauses.Single(static clause => clause.Kind == FilterClauseKind.DestinationPort).Negated);
        Assert.AreEqual(0, enriched.UserRule!.UserRuleIndex);
        Assert.HasCount(1, restored.State.IPv4.ManagedRuleAttributions);
        Assert.AreEqual(enriched.Location, restored.State.IPv4.ManagedRuleAttributions[0].Location);
        Assert.AreEqual(FilterEnrichmentStatus.Complete, restored.State.IPv4.Enrichment.Status);
        CollectionAssert.AreEqual(new[] { new FilterRuleLocation(FirewallAddressFamily.IPv4, "INPUT", 0) }, enriched.JumpPath.ToArray());
        Assert.AreEqual(FirewallProtocol.Tcp, enriched.UserRule.Specification!.Protocol);
    }

    private static RawFilterTable EmptyV6() => new()
    {
        AddressFamily = FirewallAddressFamily.IPv6,
        Chains =
        [
            Chain("INPUT", FilterChainPolicy.Drop),
            Chain("OUTPUT", FilterChainPolicy.Accept),
            Chain("FORWARD", FilterChainPolicy.Drop),
        ],
    };

    private static RawFilterTable Table(params RawFilterChain[] chains) => new()
    {
        AddressFamily = FirewallAddressFamily.IPv4,
        Chains = chains,
    };

    private static RawFilterChain Chain(string name, FilterChainPolicy policy, params RawFilterRule[] rules) => new()
    {
        Name = name,
        Policy = policy,
        Rules = rules,
    };

    private static RawFilterRule Rule(string chain, int index, FilterTarget target, params FilterClause[] clauses) => new()
    {
        Location = new FilterRuleLocation(FirewallAddressFamily.IPv4, chain, index),
        Clauses = clauses,
        Target = target,
    };

    private static UfwUserRuleTranslation Translation(int index, FirewallRuleSpecification specification, params TranslatedIptablesRule[] rules) => new()
    {
        Index = index,
        Specification = specification,
        RuleId = RuleIdentity.Compute(specification),
        IptablesRules = rules,
    };

    private static TranslatedIptablesRule Translated(string chain, FilterTarget target, params FilterClause[] clauses) => new()
    {
        ChainName = chain,
        Clauses = clauses,
        Target = target,
    };

    private static FirewallRuleSpecification Spec(FirewallAction action, string port) => RuleSpecificationNormalizer.Normalize(new FirewallRuleSpecification
    {
        Action = action,
        AddressFamily = FirewallAddressFamily.IPv4,
        Direction = FirewallDirection.In,
        Protocol = FirewallProtocol.Tcp,
        DestinationPorts = port,
    });

    private static FilterTarget Jump(string chain) => new()
    {
        Kind = FilterTargetKind.Jump,
        Name = chain,
        IsChainReference = true,
    };

    private static FilterTarget Target(string name, string? arguments = null) => new()
    {
        Kind = FilterTargetKind.Jump,
        Name = name,
        IsChainReference = false,
        Arguments = arguments,
    };

    private static FilterTarget NoneTarget() => new()
    {
        Kind = FilterTargetKind.None,
        IsChainReference = false,
    };

    private static FilterClause Protocol(string protocol) => new()
    {
        Kind = FilterClauseKind.Protocol,
        Negated = false,
        Value = protocol,
    };

    private static FilterClause Port(FilterClauseKind kind, string ports, bool negated = false) => new()
    {
        Kind = kind,
        Negated = negated,
        Value = ports,
    };

    private static FilterClause Interface(FilterClauseKind kind, string name) => new()
    {
        Kind = kind,
        Negated = false,
        Value = name,
    };

    private static FilterClause Address(FilterClauseKind kind, string address) => new()
    {
        Kind = kind,
        Negated = false,
        Value = address,
    };

    private static FilterClause Opaque(string text) => new()
    {
        Kind = FilterClauseKind.Opaque,
        Negated = false,
        Value = text,
    };
}
