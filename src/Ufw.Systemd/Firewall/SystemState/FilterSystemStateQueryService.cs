using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Systemd.Firewall.SystemState;

internal sealed class FilterSystemStateQueryService(IFilterSystemStateReader reader, IUfwExecutionGate executionGate) : IFilterSystemStateQueryService
{
    public ValueTask<IResponsePayload> GetAsync(CancellationToken cancellationToken) =>
        new(executionGate.RunAsync(GetUnsynchronizedAsync, cancellationToken));

    private async Task<IResponsePayload> GetUnsynchronizedAsync(CancellationToken cancellationToken)
    {
        FilterSystemStateReadResult result = await reader.ReadAsync(cancellationToken);
        return result switch
        {
            FilterSystemStateReadResult.Success success => new FilterSystemStateResponse(success.State),
            FilterSystemStateReadResult.Failure failure => new InternalServerErrorResponse(failure.Diagnostic),
            _ => throw new InvalidOperationException($"Unsupported filter system-state result '{result.GetType().Name}'."),
        };
    }
}
