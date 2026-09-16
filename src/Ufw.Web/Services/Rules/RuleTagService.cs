using System.Diagnostics.CodeAnalysis;
using Ufw.Web.Api.V1.Models.Rules;
using Ufw.Web.Data.Model;

namespace Ufw.Web.Services.Rules;

internal sealed class RuleTagService(IRuleTagRepository repository) : IRuleTagService
{
    public Task<RuleTagInventoryResponse> GetAsync(CancellationToken cancellationToken = default) =>
        repository.GetAsync(cancellationToken);

    public Task<RuleTagMutationResult> CreateAsync(CreateRuleTagRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TryNormalize(request.Name, request.Color, out RuleTagValues values))
        {
            return Task.FromResult(new RuleTagMutationResult(RuleTagMutationOutcome.InvalidTag));
        }
        return repository.CreateAsync(values.Name, values.Color, cancellationToken);
    }

    public Task<RuleTagMutationResult> UpdateAsync(Guid publicId, UpdateRuleTagRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TryNormalize(request.Name, request.Color, out RuleTagValues values))
        {
            return Task.FromResult(new RuleTagMutationResult(RuleTagMutationOutcome.InvalidTag));
        }
        return repository.UpdateAsync(publicId, values.Name, values.Color, cancellationToken);
    }

    public Task<RuleTagMutationResult> DeleteAsync(Guid publicId, CancellationToken cancellationToken = default) =>
        repository.DeleteAsync(publicId, cancellationToken);

    private static bool TryNormalize(string name, string color, out RuleTagValues values)
    {
        string normalizedName = name.Trim();
        if (normalizedName.Length is 0 or > RuleTagEntry.MAX_NAME_LENGTH
            || !TryNormalizeColor(color, out string? normalizedColor))
        {
            values = default;
            return false;
        }

        values = new RuleTagValues(normalizedName, normalizedColor);
        return true;
    }

    private static bool TryNormalizeColor(string value, [NotNullWhen(true)] out string? color)
    {
        string candidate = value.Trim();
        if (candidate.Length != RuleTagEntry.COLOR_LENGTH || candidate[0] != '#')
        {
            color = null;
            return false;
        }

        for (int index = 1; index < candidate.Length; index++)
        {
            char character = candidate[index];
            if (!((character >= '0' && character <= '9')
                || (character >= 'A' && character <= 'F')
                || (character >= 'a' && character <= 'f')))
            {
                color = null;
                return false;
            }
        }

        color = candidate.ToUpperInvariant();
        return true;
    }

    private readonly record struct RuleTagValues(string Name, string Color);
}
