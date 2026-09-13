using Moq;
using Ufw.Client.Api;
using Ufw.Client.KnownHosts;
using Ufw.Shared.Firewall;

namespace Ufw.Client.Tests.KnownHosts;

[TestClass]
public sealed class KnownHostInventoryServiceTests
{
    [TestMethod]
    public async Task RefreshAsync_NormalizesAndOrdersHostsAsync()
    {
        Mock<IKnownHostApiClient> api = new();
        api.Setup(client => client.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new KnownHostInventoryResponse
        {
            Hosts =
            [
                new KnownHostInventoryItem
                {
                    Id = Guid.CreateVersion7(),
                    Name = "  hidden  ",
                    Address = "2001:0db8::1",
                    AddressFamily = FirewallAddressFamily.IPv6,
                    Comment = "  secondary  ",
                    IsVisible = false,
                },
                new KnownHostInventoryItem
                {
                    Id = Guid.CreateVersion7(),
                    Name = "Primary",
                    Address = "192.0.2.129/24",
                    AddressFamily = FirewallAddressFamily.IPv4,
                    IsVisible = true,
                },
            ],
        });
        KnownHostInventoryService service = new(api.Object);

        KnownHostInventoryResponse response = await service.RefreshAsync();

        Assert.HasCount(2, response.Hosts);
        Assert.AreEqual("Primary", response.Hosts[0].Name);
        Assert.AreEqual("192.0.2.0/24", response.Hosts[0].Address);
        Assert.AreEqual("hidden", response.Hosts[1].Name);
        Assert.AreEqual("2001:db8::1", response.Hosts[1].Address);
        Assert.AreEqual("secondary", response.Hosts[1].Comment);
        Assert.AreSame(response, service.Current);
    }

    [TestMethod]
    public async Task RefreshAsync_AddressFamilyMismatch_RejectsProtocolResponseAsync()
    {
        Mock<IKnownHostApiClient> api = new();
        api.Setup(client => client.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new KnownHostInventoryResponse
        {
            Hosts =
            [
                new KnownHostInventoryItem
                {
                    Id = Guid.CreateVersion7(),
                    Name = "router",
                    Address = "192.0.2.1",
                    AddressFamily = FirewallAddressFamily.IPv6,
                },
            ],
        });
        KnownHostInventoryService service = new(api.Object);

        await Assert.ThrowsExactlyAsync<ApiProtocolException>(() => service.RefreshAsync());
    }

    [TestMethod]
    public async Task RefreshAsync_CaseInsensitiveDuplicateNames_RejectsProtocolResponseAsync()
    {
        Mock<IKnownHostApiClient> api = new();
        api.Setup(client => client.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new KnownHostInventoryResponse
        {
            Hosts =
            [
                Host("NAS", "192.0.2.1"),
                Host("nas", "192.0.2.2"),
            ],
        });
        KnownHostInventoryService service = new(api.Object);

        await Assert.ThrowsExactlyAsync<ApiProtocolException>(() => service.RefreshAsync());
    }

    [TestMethod]
    public async Task MutationMethods_ReplaceCurrentInventoryAsync()
    {
        Mock<IKnownHostApiClient> api = new();
        Guid id = Guid.CreateVersion7();
        KnownHostInventoryResponse created = new() { Hosts = [Host("one", "192.0.2.1", id)] };
        KnownHostInventoryResponse updated = new() { Hosts = [Host("renamed", "192.0.2.2", id)] };
        KnownHostInventoryResponse deleted = new();
        CreateKnownHostRequest createRequest = new() { Name = "one", Address = "192.0.2.1" };
        UpdateKnownHostRequest updateRequest = new() { Name = "renamed", Address = "192.0.2.2" };
        api.Setup(client => client.CreateAsync(createRequest, It.IsAny<CancellationToken>())).ReturnsAsync(created);
        api.Setup(client => client.UpdateAsync(id, updateRequest, It.IsAny<CancellationToken>())).ReturnsAsync(updated);
        api.Setup(client => client.DeleteAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(deleted);
        KnownHostInventoryService service = new(api.Object);

        _ = await service.CreateAsync(createRequest);
        Assert.IsNotNull(service.Current);
        Assert.AreEqual("one", service.Current.Hosts.Single().Name);

        _ = await service.UpdateAsync(id, updateRequest);
        Assert.IsNotNull(service.Current);
        Assert.AreEqual("renamed", service.Current.Hosts.Single().Name);

        _ = await service.DeleteAsync(id);
        Assert.IsNotNull(service.Current);
        Assert.IsEmpty(service.Current.Hosts);
    }

    private static KnownHostInventoryItem Host(string name, string address, Guid? id = null) => new()
    {
        Id = id ?? Guid.CreateVersion7(),
        Name = name,
        Address = address,
        AddressFamily = FirewallAddressFamily.IPv4,
        IsVisible = true,
    };
}
