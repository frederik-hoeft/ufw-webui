using Ufw.Shared.Firewall;
using Ufw.Web.Client.Features.Rules;
using Ufw.Web.Client.UI.Components.Rules;

namespace Ufw.Web.Client.Tests.UI.Components.Rules;

[TestClass]
public sealed class RuleRowInteractionStateTests
{
    [TestMethod]
    public void ToggleMetadata_RequiresAResolvableRuleOrCanonicalCommand()
    {
        RuleRowInteractionState state = new();
        RuleRowProjection unavailable = Row();

        Assert.IsFalse(RuleRowInteractionState.DetailsAvailable(unavailable));
        state.ToggleMetadata(unavailable);
        state.HandleKeyDown(unavailable, "Enter");
        Assert.IsFalse(state.MetadataExpanded);

        Assert.IsTrue(RuleRowInteractionState.DetailsAvailable(Row(ruleId: "rule-id")));
        Assert.IsTrue(RuleRowInteractionState.DetailsAvailable(Row(canonical: "ufw allow 443")));
    }

    [TestMethod]
    public void ToggleMetadata_AlternatesExpandedState()
    {
        RuleRowInteractionState state = new();
        RuleRowProjection row = Row(ruleId: "rule-id");

        state.ToggleMetadata(row);
        Assert.IsTrue(state.MetadataExpanded);

        state.ToggleMetadata(row);
        Assert.IsFalse(state.MetadataExpanded);
    }

    [TestMethod]
    public void HandleKeyDown_TogglesOnlyForKeyboardActivation()
    {
        RuleRowInteractionState state = new();
        RuleRowProjection row = Row(canonical: "ufw allow 443");

        state.HandleKeyDown(row, "Tab");
        state.HandleKeyDown(row, null);
        Assert.IsFalse(state.MetadataExpanded);

        state.HandleKeyDown(row, "Enter");
        Assert.IsTrue(state.MetadataExpanded);
        state.HandleKeyDown(row, " ");
        Assert.IsFalse(state.MetadataExpanded);
    }

    [TestMethod]
    [DataRow(true, false, false)]
    [DataRow(false, true, true)]
    [DataRow(true, true, false)]
    [DataRow(false, false, false)]
    public void DragHandle_RequiresOrderingAndAnOrderableRule(bool orderingDisabled, bool canOrder, bool enabled)
    {
        RuleRowProjection row = Row(canOrder: canOrder);
        Assert.AreEqual(enabled, RuleRowInteractionState.CanDrag(row, orderingDisabled));
        Assert.AreEqual(enabled ? "rule-drag-handle" : "rule-drag-handle rule-drag-handle-disabled", RuleRowInteractionState.DragHandleClass(row, orderingDisabled));
    }

    private static RuleRowProjection Row(string? ruleId = null, string? canonical = null, bool canOrder = true) => new(
        new ListedFirewallRule { RuleId = ruleId },
        FirewallAddressFamily.IPv4,
        OccurrenceId: 0,
        FamilyPosition: 1,
        FamilyCount: 1,
        CanOrder: canOrder,
        CanMutate: true,
        PositionChange: null,
        CanonicalCommand: canonical);
}
