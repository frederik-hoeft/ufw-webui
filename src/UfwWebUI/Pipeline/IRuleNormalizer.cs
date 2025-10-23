using UfwWebUI.Models;

namespace UfwWebUI.Pipeline;

internal interface IRuleNormalizer : IPipelineHandler
{
    void Normalize(UfwRule rule);
}
