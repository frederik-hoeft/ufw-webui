using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using UfwWebUI.Helpers;
using UfwWebUI.Models;
using UfwWebUI.Services;

namespace UfwWebUI.Pages.Rules;

[Authorize]
public class CreateModel : PageModel
{
    private readonly IUfwRuleService _ruleService;
    private readonly INetworkInterfaceService _networkInterfaceService;
    private readonly UserManager<IdentityUser> _userManager;

    public CreateModel(IUfwRuleService ruleService, INetworkInterfaceService networkInterfaceService, UserManager<IdentityUser> userManager)
    {
        _ruleService = ruleService;
        _networkInterfaceService = networkInterfaceService;
        _userManager = userManager;
    }

    [BindProperty]
    public UfwRule UfwRule { get; set; } = new UfwRule();

    public SelectList? NetworkInterfaces { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadNetworkInterfacesAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Normalize inputs before validation
        UfwRule.Source = UfwRuleHelper.NormalizeInput(UfwRule.Source);
        UfwRule.Target = UfwRuleHelper.NormalizeInput(UfwRule.Target);
        UfwRule.Ports = UfwRuleHelper.NormalizePortRange(UfwRule.Ports);
        UfwRule.Interface = string.IsNullOrWhiteSpace(UfwRule.Interface) ? null : UfwRule.Interface.Trim();
        UfwRule.Comment = UfwRule.Comment?.Trim();

        // Remove AuthorId and CreatedDate from validation
        ModelState.Remove("UfwRule.AuthorId");
        ModelState.Remove("UfwRule.CreatedDate");

        if (!ModelState.IsValid)
        {
            await LoadNetworkInterfacesAsync();
            return Page();
        }

        IdentityUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToPage("/Account/Login", new { area = "Identity" });
        }

        UfwRule.AuthorId = user.Id;
        UfwRule.CreatedDate = DateTime.UtcNow;

        await _ruleService.CreateRuleAsync(UfwRule);

        return RedirectToPage("./Index");
    }

    private async Task LoadNetworkInterfacesAsync()
    {
        IList<string> interfaces = await _networkInterfaceService.GetNetworkInterfacesAsync();
        NetworkInterfaces = new SelectList(interfaces);
    }
}
