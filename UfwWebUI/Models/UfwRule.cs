using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace UfwWebUI.Models;

public class UfwRule
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Rule Type")]
    public RuleType Type { get; set; }

    [Display(Name = "Forward")]
    public bool Forward { get; set; }

    [Display(Name = "Source IP")]
    public string? SourceIp { get; set; }

    [Display(Name = "Source Subnet")]
    public string? SourceSubnet { get; set; }

    [Display(Name = "Target IP")]
    public string? TargetIp { get; set; }

    [Display(Name = "Target Subnet")]
    public string? TargetSubnet { get; set; }

    [Display(Name = "Protocol")]
    public Protocol? Protocol { get; set; }

    [Display(Name = "Port Range Start")]
    [Range(0, 65535)]
    public int? PortRangeStart { get; set; }

    [Display(Name = "Port Range End")]
    [Range(0, 65535)]
    public int? PortRangeEnd { get; set; }

    [Display(Name = "Comment")]
    [MaxLength(500)]
    public string? Comment { get; set; }

    [Display(Name = "Enabled")]
    public bool Enabled { get; set; } = true;

    [Display(Name = "Author")]
    public string AuthorId { get; set; } = string.Empty;

    [Display(Name = "Created Date")]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public virtual IdentityUser? Author { get; set; }
}

public enum RuleType
{
    Allow,
    Deny,
    Reject
}

public enum Protocol
{
    TCP,
    UDP,
    Both
}
