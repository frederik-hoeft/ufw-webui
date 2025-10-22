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

    public async Task<IList<UfwRule>> GetAllRulesAsync()
    {
        return await _context.UfwRules
            .Include(r => r.Author)
            .OrderByDescending(r => r.CreatedDate)
            .ToListAsync();
    }

    public async Task<UfwRule?> GetRuleByIdAsync(int id)
    {
        return await _context.UfwRules
            .Include(r => r.Author)
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<UfwRule> CreateRuleAsync(UfwRule rule)
    {
        _context.UfwRules.Add(rule);
        await _context.SaveChangesAsync();
        return rule;
    }

    public async Task<UfwRule> UpdateRuleAsync(UfwRule rule)
    {
        _context.Attach(rule).State = EntityState.Modified;
        await _context.SaveChangesAsync();
        return rule;
    }

    public async Task DeleteRuleAsync(int id)
    {
        var rule = await _context.UfwRules.FindAsync(id);
        if (rule != null)
        {
            _context.UfwRules.Remove(rule);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> RuleExistsAsync(int id)
    {
        return await _context.UfwRules.AnyAsync(e => e.Id == id);
    }

    public async Task ToggleRuleAsync(int id, bool enabled)
    {
        var rule = await _context.UfwRules.FindAsync(id);
        if (rule != null)
        {
            rule.Enabled = enabled;
            await _context.SaveChangesAsync();
        }
    }
}
