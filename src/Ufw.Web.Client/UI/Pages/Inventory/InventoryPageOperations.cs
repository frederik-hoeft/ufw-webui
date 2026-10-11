using Ufw.Web.Client.Services.Errors;

namespace Ufw.Web.Client.UI.Pages.Inventory;

/// <summary>
/// Coordinates one inventory page's refresh and update operations. The latest successfully loaded inventory remains visible when an operation fails or is canceled.
/// The page owns cancellation lifetime, domain operations, dialogs, and user notifications.
/// </summary>
internal sealed class InventoryPageOperations<TInventory>(IClientErrorMapper errors) where TInventory : class
{
    public TInventory? Current { get; private set; }

    public ClientError? Error { get; private set; }

    public InventoryPageOperation Operation { get; private set; }

    public bool IsBusy => Operation != InventoryPageOperation.Idle;

    public bool IsLoading => Operation == InventoryPageOperation.Refreshing;

    public Task<bool> RefreshAsync(Func<CancellationToken, Task<TInventory>> refresh, CancellationToken cancellationToken, Action<ClientError>? onError = null)
        => RunAsync(async token => await refresh(token), InventoryPageOperation.Refreshing, cancellationToken, onError);

    public Task<bool> UpdateAsync(Func<CancellationToken, Task<TInventory?>> update, CancellationToken cancellationToken, Action<ClientError>? onError = null)
        => RunAsync(update, InventoryPageOperation.Updating, cancellationToken, onError);

    private async Task<bool> RunAsync(
        Func<CancellationToken, Task<TInventory?>> operation,
        InventoryPageOperation mode,
        CancellationToken cancellationToken,
        Action<ClientError>? onError)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (IsBusy || cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        Operation = mode;
        Error = null;
        try
        {
            TInventory? inventory = await operation(cancellationToken);
            if (inventory is null || cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            Current = inventory;
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception) when (errors.CanDescribe(exception))
        {
            ClientError error = errors.Describe(exception);
            Error = error;
            onError?.Invoke(error);
            return false;
        }
        finally
        {
            Operation = InventoryPageOperation.Idle;
        }
    }
}
