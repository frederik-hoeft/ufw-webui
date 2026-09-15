using Ufw.Client.Api;
using Ufw.Client.KnownHosts;
using Ufw.Shared.Firewall;

namespace Ufw.Client.Tests.KnownHosts;

[TestClass]
public sealed class KnownHostFieldStateTests
{
    private readonly KnownHostSuggestionService _suggestions = new();

    [TestMethod]
    public void SelectedHost_PreservesFriendlyDisplayAcrossModelRerenders()
    {
        KnownHostInventoryItem host = Host("nas1.service.home.arpa", "10.100.100.2");
        KnownHostFieldState state = new(_suggestions);
        string selectionValue = _suggestions.GetSelectionValue(host);

        string? modelValue = state.ApplyInput(selectionValue, [host], FirewallAddressFamily.IPv4);

        Assert.AreEqual(host.Address, modelValue);
        Assert.AreEqual(selectionValue, state.DisplayValue);

        state.Synchronize(modelValue, [host]);

        Assert.AreEqual(selectionValue, state.DisplayValue);
    }

    [TestMethod]
    public void SelectedHost_DropsFriendlyDisplayWhenUnderlyingLiteralChanges()
    {
        KnownHostInventoryItem host = Host("nas1.service.home.arpa", "10.100.100.2");
        KnownHostFieldState state = new(_suggestions);
        state.ApplyInput(_suggestions.GetSelectionValue(host), [host], FirewallAddressFamily.IPv4);

        state.Synchronize("10.100.100.3", [host]);

        Assert.AreEqual("10.100.100.3", state.DisplayValue);
    }

    [TestMethod]
    public void SelectedHost_DropsFriendlyDisplayWhenHostIsNoLongerSuggested()
    {
        KnownHostInventoryItem host = Host("nas1.service.home.arpa", "10.100.100.2");
        KnownHostFieldState state = new(_suggestions);
        state.ApplyInput(_suggestions.GetSelectionValue(host), [host], FirewallAddressFamily.IPv4);

        state.Synchronize(host.Address, []);

        Assert.AreEqual(host.Address, state.DisplayValue);
    }

    [TestMethod]
    public void FreeText_RemainsFreeText()
    {
        KnownHostInventoryItem host = Host("nas1.service.home.arpa", "10.100.100.2");
        KnownHostFieldState state = new(_suggestions);

        string? modelValue = state.ApplyInput("10.100.100.77", [host], FirewallAddressFamily.IPv4);

        Assert.AreEqual("10.100.100.77", modelValue);
        Assert.AreEqual("10.100.100.77", state.DisplayValue);
    }

    private static KnownHostInventoryItem Host(string name, string address) => new()
    {
        Id = Guid.CreateVersion7(),
        Name = name,
        Address = address,
        AddressFamily = FirewallAddressFamily.IPv4,
        IsVisible = true,
    };
}
