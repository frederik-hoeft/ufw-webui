using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UfwWebUI.Models;
using UfwWebUI.Services;

namespace UfwWebUI.Pages.Rules;

[Authorize]
public class EditModel : PageModel
{
    private readonly IUfwRuleService _ruleService;

    public EditModel(IUfwRuleService ruleService)
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
        UfwRule = ufwRule;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Remove AuthorId and CreatedDate from validation
        ModelState.Remove("UfwRule.AuthorId");
        ModelState.Remove("UfwRule.CreatedDate");

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            await _ruleService.UpdateRuleAsync(UfwRule);
        }
        catch (Exception)
        {
            if (!await _ruleService.RuleExistsAsync(UfwRule.Id))
            {
                return NotFound();
            }
            else
            {
                throw;
            }
        }

        return RedirectToPage("./Index");
    }
}
