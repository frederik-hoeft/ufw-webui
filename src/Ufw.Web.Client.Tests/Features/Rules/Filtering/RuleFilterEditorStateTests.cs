using Ufw.Shared.Firewall;
using Ufw.Web.Client.Features.Rules.Filtering.Actions;
using Ufw.Web.Client.Features.Rules.Filtering.Tags;
using Ufw.Web.Client.Features.Rules.Metadata;
using Ufw.Web.Client.UI.Components.Rules.Filtering;

namespace Ufw.Web.Client.Tests.Features.Rules.Filtering;

[TestClass]
public sealed class RuleFilterEditorStateTests
{
    [TestMethod]
    public void EnumSelection_ReloadsOnNewFilterButPreservesUnsavedEditsForSameFilter()
    {
        EnumRuleFilterEditorState<ActionRuleFilter, FirewallAction> state = new(
            FirewallAction.Allow, static filter => filter.Action, static value => new ActionRuleFilter(value));
        ActionRuleFilter initial = new(FirewallAction.Deny);
        state.Synchronize(initial);
        Assert.AreEqual(FirewallAction.Deny, state.Value);

        state.Value = FirewallAction.Reject;
        state.Synchronize(initial);
        Assert.AreEqual(FirewallAction.Reject, state.Build().Action);

        state.Synchronize(new ActionRuleFilter(FirewallAction.Allow));
        Assert.AreEqual(FirewallAction.Allow, state.Value);
        state.Synchronize(null);
        Assert.AreEqual(FirewallAction.Allow, state.Value);
    }

    [TestMethod]
    public async Task CatalogSelection_RefreshesDisplayAndPreservesMissingSelectionAsync()
    {
        Guid id = Guid.CreateVersion7();
        RuleTag oldTag = new(id, "Old name", "#123456");
        RuleTag currentTag = new(id, "New name", "#789ABC");
        TagRuleFilter filter = new(oldTag);
        CatalogRuleFilterEditorState<TagRuleFilter, RuleTag> state = new(
            static value => value.Tag, static item => item.Id, static item => item.Name);

        state.SetItems([currentTag], filter);
        Assert.AreSame(currentTag, state.Selected);
        state.Selected = null;
        state.Synchronize(filter);
        Assert.IsNull(state.Selected);

        RuleTag missingTag = new(Guid.CreateVersion7(), "Missing", "#000000");
        state.Synchronize(new TagRuleFilter(missingTag));
        Assert.AreSame(missingTag, state.Selected);

        state.SetItems([currentTag], filter);
        Assert.AreSame(currentTag, state.Selected);
        Assert.HasCount(1, await state.SearchAsync("NEW", CancellationToken.None));
        Assert.IsEmpty(await state.SearchAsync("none", CancellationToken.None));
    }

    [TestMethod]
    public async Task CatalogSearch_RespectsCancellationAsync()
    {
        CatalogRuleFilterEditorState<TagRuleFilter, RuleTag> state = new(
            static value => value.Tag, static item => item.Id, static item => item.Name);
        using CancellationTokenSource canceled = new();
        await canceled.CancelAsync();

        Assert.ThrowsExactly<OperationCanceledException>(() => state.SearchAsync("any", canceled.Token));
    }
}
