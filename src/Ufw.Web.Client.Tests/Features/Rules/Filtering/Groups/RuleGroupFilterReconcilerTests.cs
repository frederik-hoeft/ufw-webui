using Ufw.Shared.Firewall;
using Ufw.Web.Client.Features.Rules.Filtering;
using Ufw.Web.Client.Features.Rules.Filtering.Actions;
using Ufw.Web.Client.Features.Rules.Filtering.Groups;
using Ufw.Web.Client.Features.Rules.Metadata;

namespace Ufw.Web.Client.Tests.Features.Rules.Filtering.Groups;

[TestClass]
public sealed class RuleGroupFilterReconcilerTests
{
    [TestMethod]
    public void Reconcile_GroupStillExists_RefreshesPresentationByStableIdentity()
    {
        Guid groupId = Guid.CreateVersion7();
        RuleQuery query = new([new GroupRuleFilter(new RuleGroup(groupId, "ops", null, []))]);
        RuleGroup current = new(groupId, "operations", "renamed", []);
        RuleGroupFilterReconciler reconciler = new();

        RuleQuery result = reconciler.Reconcile(query, [current]);

        Assert.HasCount(1, result.Filters);
        GroupRuleFilter filter = Assert.IsInstanceOfType<GroupRuleFilter>(result.Filters[0]);
        Assert.AreSame(current, filter.Group);
    }

    [TestMethod]
    public void Reconcile_GroupNoLongerExists_RemovesOnlyThatFilter()
    {
        Guid groupId = Guid.CreateVersion7();
        ActionRuleFilter actionFilter = new(FirewallAction.Allow);
        RuleQuery query = new([new GroupRuleFilter(new RuleGroup(groupId, "ops", null, [])), actionFilter]);
        RuleGroupFilterReconciler reconciler = new();

        RuleQuery result = reconciler.Reconcile(query, []);

        Assert.HasCount(1, result.Filters);
        Assert.AreSame(actionFilter, result.Filters[0]);
    }

    [TestMethod]
    public void Reconcile_RemovingOnlyGroupFilter_ReturnsEmptyQuery()
    {
        RuleQuery query = new([new GroupRuleFilter(new RuleGroup(Guid.CreateVersion7(), "ops", null, []))]);
        RuleGroupFilterReconciler reconciler = new();

        RuleQuery result = reconciler.Reconcile(query, []);

        Assert.AreSame(RuleQuery.Empty, result);
    }
}
