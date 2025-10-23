using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Ufw.Web.Models;
using Ufw.Web.Pipeline;
using Ufw.Web.Services;

namespace Ufw.Web.Pages.Rules;

[Authorize]
internal sealed class CreateModel
(
    IUfwRuleService ruleService, 
    INetworkInterfaceService networkInterfaceService, 
    IRuleNormalizationService normalizationService, 
    UserManager<IdentityUser> userManager
) : PageModel
{
    [BindProperty]
    public UfwRule UfwRule { get; set; } = new();

    public SelectList? NetworkInterfaces { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadNetworkInterfacesAsync().ConfigureAwait(false);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Normalize inputs before validation using the pipeline
        normalizationService.NormalizeRule(UfwRule);

        // Remove AuthorId and CreatedDate from validation
        ModelState.Remove("UfwRule.AuthorId");
        ModelState.Remove("UfwRule.CreatedDate");

        if (!ModelState.IsValid)
        {
            await LoadNetworkInterfacesAsync().ConfigureAwait(false);
            return Page();
        }

        IdentityUser? user = await userManager.GetUserAsync(User).ConfigureAwait(false);
        if (user == null)
        {
            return RedirectToPage("/Account/Login", new { area = "Identity" });
        }

        UfwRule.AuthorId = user.Id;
        UfwRule.CreatedDate = DateTime.UtcNow;

        await ruleService.CreateRuleAsync(UfwRule).ConfigureAwait(false);

        return RedirectToPage("./Index");
    }

    private async Task LoadNetworkInterfacesAsync()
    {
        IList<string> interfaces = await networkInterfaceService.GetNetworkInterfacesAsync().ConfigureAwait(false);
        NetworkInterfaces = new SelectList(interfaces);
    }
}
