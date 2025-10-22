using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UfwWebUI.Models;
using UfwWebUI.Services;

namespace UfwWebUI.Pages.Rules;

[Authorize]
public class CreateModel : PageModel
{
    private readonly IUfwRuleService _ruleService;
    private readonly UserManager<IdentityUser> _userManager;

    public CreateModel(IUfwRuleService ruleService, UserManager<IdentityUser> userManager)
    {
        _ruleService = ruleService;
        _userManager = userManager;
    }

    [BindProperty]
    public UfwRule UfwRule { get; set; } = new UfwRule();

    public IActionResult OnGet()
    {
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

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToPage("/Account/Login", new { area = "Identity" });
        }

        UfwRule.AuthorId = user.Id;
        UfwRule.CreatedDate = DateTime.UtcNow;

        await _ruleService.CreateRuleAsync(UfwRule);

        return RedirectToPage("./Index");
    }
}
