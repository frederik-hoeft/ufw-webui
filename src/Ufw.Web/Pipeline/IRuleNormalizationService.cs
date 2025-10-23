using Ufw.Web.Models;

namespace Ufw.Web.Pipeline;

internal interface IRuleNormalizationService
{
    void NormalizeRule(UfwRule rule);
}
