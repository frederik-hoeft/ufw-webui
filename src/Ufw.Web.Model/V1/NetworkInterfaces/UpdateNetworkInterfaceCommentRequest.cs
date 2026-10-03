using Ufw.Shared.Management.NetworkInterfaces;
using Ufw.Web.Model.Validation;

namespace Ufw.Web.Model.V1.NetworkInterfaces;

public sealed class UpdateNetworkInterfaceCommentRequest
{
    [TrimmedStringLength(NetworkInterfaceLimits.MAX_COMMENT_LENGTH)]
    public string? Comment { get; init; }
}
