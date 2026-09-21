using Ufw.Shared.Firewall;
using Ufw.Web.Client.Features.Rules.Filtering.Actions;
using Ufw.Web.Client.Features.Rules.Filtering.Tags;
using Ufw.Web.Client.Features.Rules.Filtering;
using Ufw.Web.Client.Features.Rules.Metadata;

namespace Ufw.Web.Client.Tests.Features.Rules.Filtering.Tags;

[TestClass]
public sealed class RuleTagFilterReconcilerTests
{
    [TestMethod]
    public void Reconcile_TagStillExists_RefreshesPresentationByStableIdentity()
    {
        Guid tagId = Guid.CreateVersion7();
        RuleQuery query = new([new TagRuleFilter(new RuleTag(tagId, "prod", "#112233"))]);
        RuleTag current = new(tagId, "production", "#AABBCC");
        RuleTagFilterReconciler reconciler = new();

        RuleQuery result = reconciler.Reconcile(query, [current]);

        Assert.HasCount(1, result.Filters);
        TagRuleFilter filter = Assert.IsInstanceOfType<TagRuleFilter>(result.Filters[0]);
        Assert.AreSame(current, filter.Tag);
    }

    [TestMethod]
    public void Reconcile_TagNoLongerExists_RemovesOnlyThatFilter()
    {
        Guid tagId = Guid.CreateVersion7();
        ActionRuleFilter actionFilter = new(FirewallAction.Allow);
        RuleQuery query = new([new TagRuleFilter(new RuleTag(tagId, "prod", "#112233")), actionFilter]);
        RuleTagFilterReconciler reconciler = new();

        RuleQuery result = reconciler.Reconcile(query, []);

        Assert.HasCount(1, result.Filters);
        Assert.AreSame(actionFilter, result.Filters[0]);
    }

    [TestMethod]
    public void Reconcile_RemovingOnlyTagFilter_ReturnsEmptyQuery()
    {
        RuleQuery query = new([new TagRuleFilter(new RuleTag(Guid.CreateVersion7(), "prod", "#112233"))]);
        RuleTagFilterReconciler reconciler = new();

        RuleQuery result = reconciler.Reconcile(query, []);

        Assert.AreSame(RuleQuery.Empty, result);
    }
}
