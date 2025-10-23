using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Ufw.Web.Models;
using Ufw.Web.Pipeline;
using Ufw.Web.Services;

namespace Ufw.Web.Pages.Rules;

[Authorize]
internal sealed class EditModel(IUfwRuleService ruleService, INetworkInterfaceService networkInterfaceService, IRuleNormalizationService normalizationService) : PageModel
{
    [BindProperty]
    public UfwRule UfwRule { get; set; } = default!;

    public SelectList? NetworkInterfaces { get; set; }

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

        try
        {
            await ruleService.UpdateRuleAsync(UfwRule).ConfigureAwait(false);
        }
        catch (Exception)
        {
            if (!await ruleService.RuleExistsAsync(UfwRule.Id).ConfigureAwait(false))
            {
                return NotFound();
            }
            throw;
        }

        return RedirectToPage("./Index");
    }

    private async Task LoadNetworkInterfacesAsync()
    {
        List<string> interfaces = await networkInterfaceService.GetNetworkInterfacesAsync().ConfigureAwait(false);
        NetworkInterfaces = new SelectList(interfaces);
    }
}
