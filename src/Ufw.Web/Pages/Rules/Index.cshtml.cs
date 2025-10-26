using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Ufw.Web.Models;
using Ufw.Web.Services;

namespace Ufw.Web.Pages.Rules;

[Authorize]
internal sealed class IndexModel(IUfwRuleService ruleService, IUfwDisplayService displayService) : PageModel
{
    public IReadOnlyList<UfwRule> Rules { get; private set; } = [];

    public IUfwDisplayService DisplayService => displayService;

    public async Task OnGetAsync() => Rules = await ruleService.GetAllRulesAsync().ConfigureAwait(false);

    public async Task<IActionResult> OnPostToggleAsync(int id, bool enabled)
    {
        await ruleService.ToggleRuleAsync(id, enabled).ConfigureAwait(false);
        return RedirectToPage();
    }
}
