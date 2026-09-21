using Ufw.Web.Client.Features.Rules.Filtering;

namespace Ufw.Web.Client.Tests.Rules.Filtering;

[TestClass]
public sealed class RuleListInteractionStateTests
{
    [TestMethod]
    public void Resolve_NormalAllowsQueryAndOrdering()
    {
        RuleListInteractionState state = RuleListInteractionState.Resolve(queryActive: false, orderingPreviewActive: false);

        Assert.AreEqual(RuleListInteractionMode.Normal, state.Mode);
        Assert.IsTrue(state.CanChangeQuery);
        Assert.IsTrue(state.CanOrder);
    }

    [TestMethod]
    public void Resolve_FilteredDisablesOrderingButKeepsQueryEditable()
    {
        RuleListInteractionState state = RuleListInteractionState.Resolve(queryActive: true, orderingPreviewActive: false);

        Assert.AreEqual(RuleListInteractionMode.Filtered, state.Mode);
        Assert.IsTrue(state.CanChangeQuery);
        Assert.IsFalse(state.CanOrder);
    }

    [TestMethod]
    public void Resolve_OrderingPreviewDisablesQueryButKeepsPreviewOrderingAvailable()
    {
        RuleListInteractionState state = RuleListInteractionState.Resolve(queryActive: false, orderingPreviewActive: true);

        Assert.AreEqual(RuleListInteractionMode.OrderingPreview, state.Mode);
        Assert.IsFalse(state.CanChangeQuery);
        Assert.IsTrue(state.CanOrder);
    }

    [TestMethod]
    public void Resolve_RejectsImpossibleFilteredOrderingCombination()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => RuleListInteractionState.Resolve(queryActive: true, orderingPreviewActive: true));
    }
}
