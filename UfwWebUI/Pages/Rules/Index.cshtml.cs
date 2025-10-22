using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using UfwWebUI.Data;
using UfwWebUI.Models;

namespace UfwWebUI.Pages.Rules;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public IndexModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public IList<UfwRule> Rules { get; set; } = new List<UfwRule>();

    public async Task OnGetAsync()
    {
        Rules = await _context.UfwRules
            .Include(r => r.Author)
            .OrderByDescending(r => r.CreatedDate)
            .ToListAsync();
    }
}
