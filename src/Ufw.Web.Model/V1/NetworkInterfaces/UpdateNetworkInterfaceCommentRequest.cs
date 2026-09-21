using System.ComponentModel.DataAnnotations;

namespace Ufw.Web.Model.V1.NetworkInterfaces;

public sealed class UpdateNetworkInterfaceCommentRequest
{
    private const int MAX_COMMENT_LENGTH = 200;

    [StringLength(MAX_COMMENT_LENGTH)]
    public string? Comment { get; init; }
}
