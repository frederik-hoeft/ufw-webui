namespace Ufw.Web.Client.Features.Status;

internal interface IOperationalStatusService
{
    OperationalStatusSnapshot Current { get; }

    bool IsRefreshing { get; }

    event Action? Changed;

    Task RefreshAsync(CancellationToken cancellationToken = default);
}
