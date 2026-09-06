namespace Ufw.Client.Status;

internal interface IOperationalStatusService
{
    OperationalStatusSnapshot Current { get; }

    bool IsRefreshing { get; }

    event Action? Changed;

    Task RefreshAsync(CancellationToken cancellationToken = default);
}
