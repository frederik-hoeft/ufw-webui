using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Ufw.Web.Models;
using Ufw.Web.Services;

namespace Ufw.Web.Pages.Rules;

[Authorize]
internal sealed class DeleteModel(IUfwRuleService ruleService, IUfwDisplayService displayService) : PageModel
{
    [BindProperty]
    public UfwRule UfwRule { get; set; } = default!;

    public IUfwDisplayService DisplayService { get; } = displayService;

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        UfwRule? ufwRule = await ruleService.GetRuleByIdAsync(id.Value).ConfigureAwait(false);

        if (ufwRule == null)
        {
            return NotFound();
        }
        UfwRule = ufwRule;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        await ruleService.DeleteRuleAsync(id.Value).ConfigureAwait(false);

        return RedirectToPage("./Index");
    }
}
