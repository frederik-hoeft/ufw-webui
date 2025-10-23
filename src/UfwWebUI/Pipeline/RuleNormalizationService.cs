using System.Collections.Immutable;
using UfwWebUI.Models;

namespace UfwWebUI.Pipeline;

internal sealed class RuleNormalizationService(IEnumerable<IRuleNormalizer> normalizers) : IRuleNormalizationService
{
    private readonly ImmutableArray<IRuleNormalizer> _normalizers = normalizers.CreatePipeline();

    public void NormalizeRule(UfwRule rule)
    {
        foreach (IRuleNormalizer normalizer in _normalizers)
        {
            normalizer.Normalize(rule);
        }
    }
}
