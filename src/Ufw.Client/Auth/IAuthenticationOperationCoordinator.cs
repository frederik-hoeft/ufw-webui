namespace Ufw.Client.Auth;

internal interface IAuthenticationOperationCoordinator
{
    Task RunExclusiveAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);

    Task<T> RunExclusiveAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
}
