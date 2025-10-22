using UfwWebUI.Models;

namespace UfwWebUI.Pipeline;

internal interface IRuleNormalizationService
{
    void NormalizeRule(UfwRule rule);
}
