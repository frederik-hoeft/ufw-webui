using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UfwWebUI.Models;
using UfwWebUI.Services;

namespace UfwWebUI.Pages.Rules;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IUfwRuleService _ruleService;

    public IndexModel(IUfwRuleService ruleService)
    {
        _ruleService = ruleService;
    }

    public IList<UfwRule> Rules { get; set; } = new List<UfwRule>();

    public async Task OnGetAsync()
    {
        Rules = await _ruleService.GetAllRulesAsync();
    }

    public async Task<IActionResult> OnPostToggleAsync(int id, bool enabled)
    {
        await _ruleService.ToggleRuleAsync(id, enabled);
        return RedirectToPage();
    }
}
