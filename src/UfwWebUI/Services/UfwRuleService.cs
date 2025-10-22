using Microsoft.EntityFrameworkCore;
using UfwWebUI.Data;
using UfwWebUI.Models;

namespace UfwWebUI.Services;

public class UfwRuleService : IUfwRuleService
{
    private readonly ApplicationDbContext _context;

    public UfwRuleService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<UfwRule>> GetAllRulesAsync()
    {
        return await _context.UfwRules
            .Include(r => r.Author)
            .OrderByDescending(r => r.CreatedDate)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task<UfwRule?> GetRuleByIdAsync(int id)
    {
        return await _context.UfwRules
            .Include(r => r.Author)
            .FirstOrDefaultAsync(m => m.Id == id)
            .ConfigureAwait(false);
    }

    public async Task CreateRuleAsync(UfwRule rule)
    {
        _context.UfwRules.Add(rule);
        await _context.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task UpdateRuleAsync(UfwRule rule)
    {
        _context.Attach(rule).State = EntityState.Modified;
        await _context.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task DeleteRuleAsync(int id)
    {
        UfwRule? rule = await _context.UfwRules.FindAsync(id).ConfigureAwait(false);
        if (rule != null)
        {
            _context.UfwRules.Remove(rule);
            await _context.SaveChangesAsync().ConfigureAwait(false);
        }
    }

    public async Task<bool> RuleExistsAsync(int id)
    {
        return await _context.UfwRules.AnyAsync(e => e.Id == id).ConfigureAwait(false);
    }

    public async Task ToggleRuleAsync(int id, bool enabled)
    {
        UfwRule? rule = await _context.UfwRules.FindAsync(id).ConfigureAwait(false);
        if (rule != null)
        {
            rule.Enabled = enabled;
            await _context.SaveChangesAsync().ConfigureAwait(false);
        }
    }
}
