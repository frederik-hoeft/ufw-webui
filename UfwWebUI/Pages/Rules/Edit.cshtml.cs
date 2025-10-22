using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using UfwWebUI.Data;
using UfwWebUI.Models;

namespace UfwWebUI.Pages.Rules;

[Authorize]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public EditModel(ApplicationDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public UfwRule UfwRule { get; set; } = default!;

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var ufwRule = await _context.UfwRules.FirstOrDefaultAsync(m => m.Id == id);
        if (ufwRule == null)
        {
            return NotFound();
        }
        UfwRule = ufwRule;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        _context.Attach(UfwRule).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!UfwRuleExists(UfwRule.Id))
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

    private bool UfwRuleExists(int id)
    {
        return _context.UfwRules.Any(e => e.Id == id);
    }
}
