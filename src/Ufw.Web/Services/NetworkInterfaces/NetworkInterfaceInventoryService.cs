using Ufw.Web.Api.V1.Models.NetworkInterfaces;
using Ufw.Web.Data.Model;

namespace Ufw.Web.Services.NetworkInterfaces;

internal sealed class NetworkInterfaceInventoryService(
    IDaemonNetworkInterfaceSource daemonSource,
    INetworkInterfaceInventoryRepository repository,
    TimeProvider timeProvider) : INetworkInterfaceInventoryService
{
    public Task<NetworkInterfaceInventoryResponse> GetCachedAsync(CancellationToken cancellationToken = default) =>
        repository.GetAsync(cancellationToken);

    public async Task<NetworkInterfaceInventoryResponse> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> currentNames = await daemonSource.GetInterfaceNamesAsync(cancellationToken);
        return await repository.ReconcileAsync(currentNames, timeProvider.GetUtcNow(), cancellationToken);
    }

    public Task<NetworkInterfaceInventoryResponse?> UpdateCommentAsync(Guid publicId, string? comment, CancellationToken cancellationToken = default) =>
        repository.UpdateCommentAsync(publicId, NormalizeComment(comment), cancellationToken);

    public Task<NetworkInterfaceInventoryResponse?> UpdateVisibilityAsync(Guid publicId, bool isVisible, CancellationToken cancellationToken = default) =>
        repository.UpdateVisibilityAsync(publicId, isVisible, cancellationToken);

    private static string? NormalizeComment(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return null;
        }

        string normalized = comment.Trim();
        if (normalized.Length > NetworkInterfaceEntry.MAX_COMMENT_LENGTH)
        {
            throw new ArgumentException($"Interface comments must not exceed {NetworkInterfaceEntry.MAX_COMMENT_LENGTH} characters.", nameof(comment));
        }

        return normalized;
    }
}
