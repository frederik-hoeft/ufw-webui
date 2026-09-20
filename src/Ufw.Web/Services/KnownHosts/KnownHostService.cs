using Ufw.Shared.Firewall;
using Ufw.Web.Api.V1.Models.KnownHosts;
using Ufw.Web.Data.Model;

namespace Ufw.Web.Services.KnownHosts;

internal sealed class KnownHostService(IKnownHostRepository repository) : IKnownHostService
{
    public Task<KnownHostInventoryResponse> GetAsync(CancellationToken cancellationToken = default) =>
        repository.GetAsync(cancellationToken);

    public Task<KnownHostMutationResult> CreateAsync(CreateKnownHostRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TryNormalize(request.Name, request.Address, request.Comment, out KnownHostValues values))
        {
            return Task.FromResult(new KnownHostMutationResult(KnownHostMutationOutcome.InvalidAddress));
        }

        return repository.CreateAsync(values.Name, values.NormalizedName, values.Address, values.Comment, request.IsVisible, cancellationToken);
    }

    public Task<KnownHostMutationResult> UpdateAsync(Guid publicId, UpdateKnownHostRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TryNormalize(request.Name, request.Address, request.Comment, out KnownHostValues values))
        {
            return Task.FromResult(new KnownHostMutationResult(KnownHostMutationOutcome.InvalidAddress));
        }

        return repository.UpdateAsync(publicId, values.Name, values.NormalizedName, values.Address, values.AddressFamily, values.Comment, request.IsVisible, cancellationToken);
    }

    public Task<KnownHostMutationResult> DeleteAsync(Guid publicId, CancellationToken cancellationToken = default) =>
        repository.DeleteAsync(publicId, cancellationToken);

    private static bool TryNormalize(string name, string address, string? comment, out KnownHostValues values)
    {
        string normalizedName = name.Trim();
        string lookupName = normalizedName.ToUpperInvariant();
        if (normalizedName.Length is 0 or > KnownHostEntry.MAX_NAME_LENGTH
            || lookupName.Length > KnownHostEntry.MAX_NAME_LENGTH
            || !FirewallAddressValue.TryNormalizeLiteral(address, out string? normalizedAddress, out FirewallAddressFamily addressFamily))
        {
            values = default;
            return false;
        }

        string? normalizedComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (normalizedComment?.Length > KnownHostEntry.MAX_COMMENT_LENGTH)
        {
            values = default;
            return false;
        }

        values = new KnownHostValues(normalizedName, lookupName, normalizedAddress, addressFamily, normalizedComment);
        return true;
    }

    private readonly record struct KnownHostValues(string Name, string NormalizedName, string Address, FirewallAddressFamily AddressFamily, string? Comment);
}
