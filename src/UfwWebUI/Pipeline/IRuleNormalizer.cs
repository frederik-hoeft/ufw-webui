using UfwWebUI.Models;

namespace UfwWebUI.Pipeline;

public interface IRuleNormalizer : IPipelineHandler
{
    void Normalize(UfwRule rule);
}
