using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using UfwWebUI.Models;
using UfwWebUI.Pipeline;
using UfwWebUI.Services;

namespace UfwWebUI.Pages.Rules;

[Authorize]
public class CreateModel : PageModel
{
    private readonly IUfwRuleService _ruleService;
    private readonly INetworkInterfaceService _networkInterfaceService;
    private readonly IRuleNormalizationService _normalizationService;
    private readonly UserManager<IdentityUser> _userManager;

    public CreateModel(IUfwRuleService ruleService, INetworkInterfaceService networkInterfaceService, IRuleNormalizationService normalizationService, UserManager<IdentityUser> userManager)
    {
        _ruleService = ruleService;
        _networkInterfaceService = networkInterfaceService;
        _normalizationService = normalizationService;
        _userManager = userManager;
    }

    [BindProperty]
    public UfwRule UfwRule { get; set; } = new UfwRule();

    public SelectList? NetworkInterfaces { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
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

        IdentityUser? user = await _userManager.GetUserAsync(User).ConfigureAwait(false);
        if (user == null)
        {
            return RedirectToPage("/Account/Login", new { area = "Identity" });
        }

        UfwRule.AuthorId = user.Id;
        UfwRule.CreatedDate = DateTime.UtcNow;

        await _ruleService.CreateRuleAsync(UfwRule).ConfigureAwait(false);

        return RedirectToPage("./Index");
    }

    private async Task LoadNetworkInterfacesAsync()
    {
        IList<string> interfaces = await _networkInterfaceService.GetNetworkInterfacesAsync().ConfigureAwait(false);
        NetworkInterfaces = new SelectList(interfaces);
    }
}
