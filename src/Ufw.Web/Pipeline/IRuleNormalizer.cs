using Ufw.Web.Models;

namespace Ufw.Web.Pipeline;

internal interface IRuleNormalizer : IPipelineHandler
{
    void Normalize(UfwRule rule);
}
