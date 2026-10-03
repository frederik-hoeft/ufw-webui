using System.ComponentModel.DataAnnotations;
using Ufw.Web.Model.Validation;

namespace Ufw.Web.Model.V1.NetworkInterfaces;

public sealed class CleanupNetworkInterfacesRequest
{
    [Required]
    [MinLength(1)]
    [NoEmptyGuids]
    public IReadOnlyList<Guid> InterfaceIds { get; init; } = [];
}
