using Ufw.Web.Client.Features.Rules;

namespace Ufw.Web.Client.Tests.Features.Rules;

[TestClass]
public sealed class RulesPageInteractionStateTests
{
    [TestMethod]
    public void DeleteFlow_ConfirmsDirectlyFromDialogAndReturnsToIdle()
    {
        RulesPageInteractionState dialog = RulesPageInteractionState.Initial.MoveNext(new RulesPageInteractionTransition.DeleteDialogOpened());

        Assert.IsTrue(dialog.IsBusy);
        Assert.IsFalse(dialog.CanMutateFirewall);
        RulesPageInteractionState deleting = dialog.MoveNext(new RulesPageInteractionTransition.DeleteConfirmed());
        Assert.ThrowsExactly<InvalidOperationException>(() => dialog.MoveNext(new RulesPageInteractionTransition.ReorderStarted()));

        Assert.IsTrue(deleting.IsDeleting);
        Assert.AreSame(RulesPageInteractionState.Initial, deleting.MoveNext(new RulesPageInteractionTransition.DeleteCompleted()));
    }

    [TestMethod]
    public void MetadataFlow_ModelsSaveAsNestedDialogState()
    {
        RulesPageInteractionState dialog = RulesPageInteractionState.Initial.MoveNext(new RulesPageInteractionTransition.MetadataDialogOpened());
        RulesPageInteractionState saving = dialog.MoveNext(new RulesPageInteractionTransition.MetadataSaveStarted());

        Assert.AreEqual(RulesPageInteractionMode.MetadataSaving, saving.Mode);
        Assert.IsFalse(saving.CanEditMetadata);
        Assert.ThrowsExactly<InvalidOperationException>(() => saving.MoveNext(new RulesPageInteractionTransition.MetadataDialogClosed()));

        RulesPageInteractionState restoredDialog = saving.MoveNext(new RulesPageInteractionTransition.MetadataSaveCompleted());
        Assert.AreEqual(RulesPageInteractionMode.MetadataDialog, restoredDialog.Mode);
        Assert.AreSame(RulesPageInteractionState.Initial, restoredDialog.MoveNext(new RulesPageInteractionTransition.MetadataDialogClosed()));
    }

    [TestMethod]
    public void ReorderFlow_IsExclusive()
    {
        RulesPageInteractionState reordering = RulesPageInteractionState.Initial.MoveNext(new RulesPageInteractionTransition.ReorderStarted());

        Assert.IsTrue(reordering.IsBusy);
        Assert.IsTrue(reordering.IsReordering);
        Assert.IsFalse(reordering.CanMutateFirewall);
        Assert.IsFalse(reordering.CanEditMetadata);
        Assert.IsFalse(reordering.CanPreviewOrdering);
        Assert.ThrowsExactly<InvalidOperationException>(() => reordering.MoveNext(new RulesPageInteractionTransition.MetadataDialogOpened()));
        Assert.AreSame(RulesPageInteractionState.Initial, reordering.MoveNext(new RulesPageInteractionTransition.ReorderCompleted()));
    }
}
