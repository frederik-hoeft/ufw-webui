using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using UfwWebUI.Data;
using UfwWebUI.Models;

namespace UfwWebUI.Pages.Rules;

[Authorize]
public class DeleteModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public DeleteModel(ApplicationDbContext context)
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

        var ufwRule = await _context.UfwRules.FindAsync(id);
        if (ufwRule != null)
        {
            UfwRule = ufwRule;
            _context.UfwRules.Remove(UfwRule);
            await _context.SaveChangesAsync();
        }

        return RedirectToPage("./Index");
    }
}
