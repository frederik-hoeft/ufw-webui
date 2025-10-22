using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UfwWebUI.Models;
using UfwWebUI.Services;

namespace UfwWebUI.Pages.Rules;

[Authorize]
public class DeleteModel : PageModel
{
    private readonly IUfwRuleService _ruleService;

    public DeleteModel(IUfwRuleService ruleService)
    {
        _ruleService = ruleService;
    }

    [BindProperty]
    public UfwRule UfwRule { get; set; } = default!;

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var ufwRule = await _ruleService.GetRuleByIdAsync(id.Value);

        if (ufwRule == null)
        {
            return NotFound();
        }
        else
        {
            UfwRule = ufwRule;
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        await _ruleService.DeleteRuleAsync(id.Value);

        return RedirectToPage("./Index");
    }
}
