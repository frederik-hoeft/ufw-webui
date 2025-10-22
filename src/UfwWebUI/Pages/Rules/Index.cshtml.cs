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

    public IList<UfwRule> Rules { get; private set; } = new List<UfwRule>();

    public async Task OnGetAsync()
    {
        Rules = await _ruleService.GetAllRulesAsync().ConfigureAwait(false);
    }

    public async Task<IActionResult> OnPostToggleAsync(int id, bool enabled)
    {
        await _ruleService.ToggleRuleAsync(id, enabled).ConfigureAwait(false);
        return RedirectToPage();
    }
}
