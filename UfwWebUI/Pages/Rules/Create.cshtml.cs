using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UfwWebUI.Data;
using UfwWebUI.Models;

namespace UfwWebUI.Pages.Rules;

[Authorize]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;

    public CreateModel(ApplicationDbContext context, UserManager<IdentityUser> userManager)
    {
        _context = context;
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

        _context.UfwRules.Add(UfwRule);
        await _context.SaveChangesAsync();

        return RedirectToPage("./Index");
    }
}
