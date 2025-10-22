using Microsoft.EntityFrameworkCore;
using UfwWebUI.Data;
using UfwWebUI.Models;

namespace UfwWebUI.Services;

internal sealed class UfwRuleService(ApplicationDbContext context) : IUfwRuleService
{
    public Task<List<UfwRule>> GetAllRulesAsync() => context.UfwRules
        .Include(static r => r.Author)
        .OrderByDescending(static r => r.CreatedDate)
        .ToListAsync();

    public Task<UfwRule?> GetRuleByIdAsync(int id) => context.UfwRules
        .Include(static r => r.Author)
        .FirstOrDefaultAsync(m => m.Id == id);

    public async Task CreateRuleAsync(UfwRule rule)
    {
        context.UfwRules.Add(rule);
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task UpdateRuleAsync(UfwRule rule)
    {
        context.Attach(rule).State = EntityState.Modified;
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task DeleteRuleAsync(int id)
    {
        UfwRule? rule = await context.UfwRules.FindAsync(id).ConfigureAwait(false);
        if (rule != null)
        {
            context.UfwRules.Remove(rule);
            await context.SaveChangesAsync().ConfigureAwait(false);
        }
    }

    public Task<bool> RuleExistsAsync(int id) => context.UfwRules.AnyAsync(e => e.Id == id);

    public async Task ToggleRuleAsync(int id, bool enabled)
    {
        UfwRule? rule = await context.UfwRules.FindAsync(id).ConfigureAwait(false);
        if (rule != null)
        {
            rule.Enabled = enabled;
            await context.SaveChangesAsync().ConfigureAwait(false);
        }
    }
}
