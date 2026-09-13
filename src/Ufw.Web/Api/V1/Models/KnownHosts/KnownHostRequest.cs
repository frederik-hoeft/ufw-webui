using System.ComponentModel.DataAnnotations;
using Ufw.Web.Data.Model;

namespace Ufw.Web.Api.V1.Models.KnownHosts;

public abstract class KnownHostRequest
{
    [Required]
    [StringLength(KnownHostEntry.MAX_NAME_LENGTH)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [StringLength(KnownHostEntry.MAX_ADDRESS_LENGTH)]
    public string Address { get; init; } = string.Empty;

    [StringLength(KnownHostEntry.MAX_COMMENT_LENGTH)]
    public string? Comment { get; init; }

    public bool IsVisible { get; init; } = true;
}
