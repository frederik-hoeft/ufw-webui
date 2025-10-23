using Ufw.Web.Models;

namespace Ufw.Web.Services;

internal interface IUfwRuleService
{
    Task<List<UfwRule>> GetAllRulesAsync();
    Task<UfwRule?> GetRuleByIdAsync(int id);
    Task CreateRuleAsync(UfwRule rule);
    Task UpdateRuleAsync(UfwRule rule);
    Task DeleteRuleAsync(int id);
    Task<bool> RuleExistsAsync(int id);
    Task ToggleRuleAsync(int id, bool enabled);
}
