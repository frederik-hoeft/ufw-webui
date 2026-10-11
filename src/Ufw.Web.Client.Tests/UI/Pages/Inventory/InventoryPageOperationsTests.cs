using Moq;
using Ufw.Web.Client.Services.Errors;
using Ufw.Web.Client.UI.Pages.Inventory;

namespace Ufw.Web.Client.Tests.UI.Pages.Inventory;

[TestClass]
public sealed class InventoryPageOperationsTests
{
    [TestMethod]
    public async Task RefreshAsync_LoadsInventoryAndResetsOperationAsync()
    {
        InventoryPageOperations<string> operations = CreateOperations();

        bool loaded = await operations.RefreshAsync(_ => Task.FromResult("inventory"), CancellationToken.None);

        Assert.IsTrue(loaded);
        Assert.AreEqual("inventory", operations.Current);
        Assert.IsNull(operations.Error);
        Assert.AreEqual(InventoryPageOperation.Idle, operations.Operation);
    }

    [TestMethod]
    public async Task RefreshAsync_BlocksConcurrentUpdatesAndRefreshesAsync()
    {
        InventoryPageOperations<string> operations = CreateOperations();
        TaskCompletionSource<string> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> pending = operations.RefreshAsync(_ => completion.Task, CancellationToken.None);
        int competingOperations = 0;

        Assert.IsTrue(operations.IsBusy);
        Assert.IsTrue(operations.IsLoading);
        bool refreshAccepted = await operations.RefreshAsync(_ =>
        {
            competingOperations++;
            return Task.FromResult("refreshed");
        }, CancellationToken.None);
        bool mutationAccepted = await operations.UpdateAsync(_ =>
        {
            competingOperations++;
            return Task.FromResult<string?>("updated");
        }, CancellationToken.None);

        Assert.IsFalse(refreshAccepted);
        Assert.IsFalse(mutationAccepted);
        Assert.AreEqual(0, competingOperations);
        completion.SetResult("initial");
        Assert.IsTrue(await pending);
        Assert.IsFalse(operations.IsBusy);
        Assert.AreEqual("initial", operations.Current);
    }

    [TestMethod]
    public async Task UpdateAsync_ReservesOperationAcrossDialogAndDoesNotOverwriteOnCancelAsync()
    {
        InventoryPageOperations<string> operations = CreateOperations();
        _ = await operations.RefreshAsync(_ => Task.FromResult("original"), CancellationToken.None);
        TaskCompletionSource<string?> dialog = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> pending = operations.UpdateAsync(_ => dialog.Task, CancellationToken.None);

        Assert.AreEqual(InventoryPageOperation.Updating, operations.Operation);
        Assert.IsTrue(operations.IsBusy);
        Assert.IsFalse(operations.IsLoading);
        dialog.SetResult(null);
        Assert.IsFalse(await pending);
        Assert.AreEqual("original", operations.Current);
        Assert.IsFalse(operations.IsBusy);
        Assert.IsNull(operations.Error);
    }

    [TestMethod]
    public async Task UpdateAsync_ErrorPreservesInventoryAndNotifiesOnlyForMappedFailureAsync()
    {
        ClientError mapped = new(ClientErrorKind.Unavailable, "server unavailable", Retryable: true);
        Mock<IClientErrorMapper> errors = new();
        errors.Setup(mapper => mapper.CanDescribe(It.IsAny<HttpRequestException>())).Returns(true);
        errors.Setup(mapper => mapper.Describe(It.IsAny<HttpRequestException>())).Returns(mapped);
        InventoryPageOperations<string> operations = new(errors.Object);
        _ = await operations.RefreshAsync(_ => Task.FromResult("last confirmed"), CancellationToken.None);
        List<ClientError> notifications = [];

        bool succeeded = await operations.UpdateAsync(_ => Task.FromException<string?>(new HttpRequestException("failed")), CancellationToken.None, notifications.Add);

        Assert.IsFalse(succeeded);
        Assert.AreEqual("last confirmed", operations.Current);
        Assert.AreSame(mapped, operations.Error);
        Assert.AreEqual(InventoryPageOperation.Idle, operations.Operation);
        CollectionAssert.AreEqual(new[] { mapped }, notifications);

        Assert.IsTrue(await operations.RefreshAsync(_ => Task.FromResult("current"), CancellationToken.None));
        Assert.IsNull(operations.Error);
        Assert.AreEqual("current", operations.Current);
        Assert.HasCount(1, notifications);
    }

    [TestMethod]
    public async Task RefreshAsync_CancellationPreservesPreviousInventoryWithoutErrorAsync()
    {
        InventoryPageOperations<string> operations = CreateOperations();
        _ = await operations.RefreshAsync(_ => Task.FromResult("confirmed"), CancellationToken.None);
        using CancellationTokenSource lifetime = new();
        TaskCompletionSource<string> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> pending = operations.RefreshAsync(_ => completion.Task, lifetime.Token);
        await lifetime.CancelAsync();
        completion.SetException(new OperationCanceledException(lifetime.Token));

        Assert.IsFalse(await pending);
        Assert.AreEqual("confirmed", operations.Current);
        Assert.IsNull(operations.Error);
        Assert.IsFalse(operations.IsBusy);
        Assert.IsFalse(await operations.UpdateAsync(_ => Task.FromResult<string?>("ignored"), lifetime.Token));
    }

    [TestMethod]
    public async Task UpdateAsync_UnexpectedFailurePropagatesAndReleasesOperationAsync()
    {
        InventoryPageOperations<string> operations = CreateOperations();
        InvalidOperationException failure = new("unexpected");

        InvalidOperationException actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => operations.UpdateAsync(_ => Task.FromException<string?>(failure), CancellationToken.None));

        Assert.AreSame(failure, actual);
        Assert.IsFalse(operations.IsBusy);
        Assert.IsNull(operations.Error);
    }

    private static InventoryPageOperations<string> CreateOperations() => new(new Mock<IClientErrorMapper>().Object);
}
