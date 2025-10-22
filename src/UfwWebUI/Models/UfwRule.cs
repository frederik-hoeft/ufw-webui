using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using UfwWebUI.Validation;

namespace UfwWebUI.Models;

internal sealed class UfwRule
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Rule Type")]
    public RuleType Type { get; set; }

    [Display(Name = "Route (Forward)")]
    public bool IsRoute { get; set; }

    [Display(Name = "Direction")]
    public Direction Direction { get; set; } = Direction.In;

    [Display(Name = "Interface")]
    public string? Interface { get; set; }

    [Display(Name = "Source")]
    [ValidIPv4AddressOrAny]
    public string? Source { get; set; }

    [Display(Name = "Target")]
    [ValidIPv4AddressOrAny]
    public string? Target { get; set; }

    [Display(Name = "Protocol")]
    public UfwProtocol Protocol { get; set; } = UfwProtocol.Any;

    [Display(Name = "Ports")]
    [ValidPortRange]
    public string? Ports { get; set; }

    [Display(Name = "Comment")]
    [MaxLength(500)]
    public string? Comment { get; set; }

    [Display(Name = "Enabled")]
    public bool Enabled { get; set; } = true;

    [Display(Name = "Author")]
    public string AuthorId { get; set; } = string.Empty;

    [Display(Name = "Created Date")]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public IdentityUser? Author { get; set; }
}