using System.Collections.Immutable;
using UfwWebUI.Models;

namespace UfwWebUI.Pipeline;

public interface IRuleNormalizationService
{
    void NormalizeRule(UfwRule rule);
}

public sealed class RuleNormalizationService : IRuleNormalizationService
{
    private readonly ImmutableArray<IRuleNormalizer> _normalizers;

    public RuleNormalizationService(IEnumerable<IRuleNormalizer> normalizers)
    {
        _normalizers = normalizers.CreatePipeline();
    }

    public void NormalizeRule(UfwRule rule)
    {
        foreach (IRuleNormalizer normalizer in _normalizers)
        {
            normalizer.Normalize(rule);
        }
    }
}
