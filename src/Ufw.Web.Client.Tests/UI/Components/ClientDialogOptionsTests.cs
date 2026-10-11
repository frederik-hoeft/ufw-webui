using MudBlazor;
using Ufw.Web.Client.UI.Components;

namespace Ufw.Web.Client.Tests.UI.Components;

[TestClass]
public sealed class ClientDialogOptionsTests
{
    [TestMethod]
    public void StandardPresets_PreserveModalPoliciesAndWidths()
    {
        AssertModal(ClientDialogOptions.Compact, MaxWidth.ExtraSmall);
        AssertModal(ClientDialogOptions.Standard, MaxWidth.Small);
        AssertModal(ClientDialogOptions.Wide, MaxWidth.Medium);
    }

    [TestMethod]
    public void FocusedPresets_RequestFirstChildFocusWithoutChangingOtherPolicies()
    {
        AssertModal(ClientDialogOptions.FocusedCompact, MaxWidth.ExtraSmall, DefaultFocus.FirstChild);
        AssertModal(ClientDialogOptions.FocusedStandard, MaxWidth.Small, DefaultFocus.FirstChild);
    }

    [TestMethod]
    public void FilterEditor_PreservesDefaultBackdropAndEscapePolicies()
    {
        DialogOptions options = ClientDialogOptions.FilterEditor;

        Assert.IsTrue(options.CloseButton);
        Assert.IsTrue(options.FullWidth);
        Assert.AreEqual(MaxWidth.Small, options.MaxWidth);
        Assert.IsNull(options.BackdropClick);
        Assert.IsNull(options.CloseOnEscapeKey);
        Assert.IsNull(options.DefaultFocus);
    }

    [TestMethod]
    public void Presets_ReturnIndependentInstances()
    {
        DialogOptions first = ClientDialogOptions.Standard;
        DialogOptions second = ClientDialogOptions.Standard;

        Assert.AreNotSame(first, second);
    }

    private static void AssertModal(DialogOptions options, MaxWidth width, DefaultFocus? focus = null)
    {
        Assert.IsFalse(options.BackdropClick);
        Assert.IsTrue(options.CloseButton);
        Assert.IsTrue(options.CloseOnEscapeKey);
        Assert.IsTrue(options.FullWidth);
        Assert.AreEqual(width, options.MaxWidth);
        Assert.AreEqual(focus, options.DefaultFocus);
    }
}
