using UfwWebUI.Models;

namespace UfwWebUI.Services;

public interface IUfwRuleService
{
    Task<IList<UfwRule>> GetAllRulesAsync();
    Task<UfwRule?> GetRuleByIdAsync(int id);
    Task<UfwRule> CreateRuleAsync(UfwRule rule);
    Task<UfwRule> UpdateRuleAsync(UfwRule rule);
    Task DeleteRuleAsync(int id);
    Task<bool> RuleExistsAsync(int id);
    Task ToggleRuleAsync(int id, bool enabled);
}
