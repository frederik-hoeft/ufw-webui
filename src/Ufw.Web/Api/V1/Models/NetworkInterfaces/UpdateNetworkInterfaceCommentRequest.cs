using System.ComponentModel.DataAnnotations;
using Ufw.Web.Data.Model;

namespace Ufw.Web.Api.V1.Models.NetworkInterfaces;

public sealed class UpdateNetworkInterfaceCommentRequest
{
    [StringLength(NetworkInterfaceEntry.MAX_COMMENT_LENGTH)]
    public string? Comment { get; init; }
}
