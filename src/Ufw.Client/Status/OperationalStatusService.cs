using Ufw.Client.Api;
using Ufw.Client.Errors;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Security.Intent;

namespace Ufw.Client.Status;

internal sealed class OperationalStatusService(
    IUfwApiClient apiClient,
    IClientErrorMapper clientErrors,
    TimeProvider timeProvider) : IOperationalStatusService, IDisposable
{
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    public OperationalStatusSnapshot Current { get; private set; } = OperationalStatusSnapshot.Unknown;

    public bool IsRefreshing { get; private set; }

    public event Action? Changed;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            IsRefreshing = true;
            Changed?.Invoke();

            long startedAt = timeProvider.GetTimestamp();
            Task<IntentContextResponse> intentContextTask = apiClient.GetIntentContextAsync(cancellationToken);
            Task<RuleListResponse> rulesTask = apiClient.GetRulesAsync(cancellationToken);

            try
            {
                await Task.WhenAll(intentContextTask, rulesTask);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // Individual task failures are projected below so partial operational state remains visible.
            }

            IntentContextResponse? intentContext = intentContextTask.Status == TaskStatus.RanToCompletion
                ? await intentContextTask
                : null;
            RuleListResponse? rules = rulesTask.Status == TaskStatus.RanToCompletion
                ? await rulesTask
                : null;

            ClientError? intentError = DescribeFailure(intentContextTask);
            ClientError? firewallError = DescribeFailure(rulesTask);

            Current = new OperationalStatusSnapshot
            {
                DaemonBackedApi = intentContext is not null || rules is not null
                    ? OperationalAvailability.Available
                    : OperationalAvailability.Unavailable,
                FirewallSnapshot = rules is not null
                    ? OperationalAvailability.Available
                    : OperationalAvailability.Unavailable,
                FirewallActive = rules?.Active,
                RuleCount = rules?.Rules.Count,
                IntentProtocolVersion = intentContext?.ProtocolVersion,
                IntentProtocolCompatible = intentContext is null
                    ? null
                    : intentContext.ProtocolVersion == IntentProtocol.VERSION,
                DeploymentId = intentContext?.DeploymentId,
                CheckedAt = timeProvider.GetUtcNow(),
                RoundTrip = timeProvider.GetElapsedTime(startedAt),
                IntentContextError = intentError,
                FirewallError = firewallError,
            };
        }
        finally
        {
            IsRefreshing = false;
            Changed?.Invoke();
            _refreshGate.Release();
        }
    }

    private ClientError? DescribeFailure(Task task)
    {
        if (task.Exception is null)
        {
            return null;
        }

        Exception exception = task.Exception.InnerExceptions.Count == 1
            ? task.Exception.InnerException!
            : task.Exception;
        return clientErrors.Describe(exception);
    }

    public void Dispose() => _refreshGate.Dispose();
}
