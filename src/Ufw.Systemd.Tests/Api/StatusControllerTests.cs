using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Systemd.Api.Controllers;

namespace Ufw.Systemd.Tests.Api;

[TestClass]
public sealed class StatusControllerTests
{
    [TestMethod]
    public async Task GetStatusAsync_ReturnsEmptySuccessWithoutDependenciesAsync()
    {
        StatusController controller = new();

        IResponsePayload response = await controller.GetStatusAsync(CancellationToken.None);

        Assert.IsInstanceOfType<OkResponse>(response);
    }

    [TestMethod]
    public async Task GetStatusAsync_ObservesCallerCancellationAsync()
    {
        StatusController controller = new();
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await controller.GetStatusAsync(cancellation.Token));
    }

    [TestMethod]
    public async Task DefaultServiceProvider_ResolvesStatusControllerAsync()
    {
        await using DefaultServiceProvider serviceProvider = new();

        StatusController controller = serviceProvider.GetService<StatusController>();

        Assert.IsNotNull(controller);
    }
}
