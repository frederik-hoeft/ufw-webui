using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using UfwWebUI.Models;
using UfwWebUI.Pipeline;
using UfwWebUI.Services;

namespace UfwWebUI.Pages.Rules;

[Authorize]
public class EditModel : PageModel
{
    private readonly IUfwRuleService _ruleService;
    private readonly INetworkInterfaceService _networkInterfaceService;
    private readonly IRuleNormalizationService _normalizationService;

    public EditModel(IUfwRuleService ruleService, INetworkInterfaceService networkInterfaceService, IRuleNormalizationService normalizationService)
    {
        _ruleService = ruleService;
        _networkInterfaceService = networkInterfaceService;
        _normalizationService = normalizationService;
    }

    [BindProperty]
    public UfwRule UfwRule { get; set; } = default!;

    public SelectList? NetworkInterfaces { get; set; }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        UfwRule? ufwRule = await _ruleService.GetRuleByIdAsync(id.Value).ConfigureAwait(false);
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
        _normalizationService.NormalizeRule(UfwRule);

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
            await _ruleService.UpdateRuleAsync(UfwRule).ConfigureAwait(false);
        }
        catch (Exception)
        {
            if (!await _ruleService.RuleExistsAsync(UfwRule.Id).ConfigureAwait(false))
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

    private async Task LoadNetworkInterfacesAsync()
    {
        IList<string> interfaces = await _networkInterfaceService.GetNetworkInterfacesAsync().ConfigureAwait(false);
        NetworkInterfaces = new SelectList(interfaces);
    }
}
