using System.ComponentModel.DataAnnotations;
using Ufw.Shared.Management.NetworkInterfaces;

namespace Ufw.Web.Model.V1.NetworkInterfaces;

public sealed class UpdateNetworkInterfaceCommentRequest
{
    [StringLength(NetworkInterfaceLimits.MAX_COMMENT_LENGTH)]
    public string? Comment { get; init; }
}
