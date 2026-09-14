namespace Ufw.Web.Services.KnownHosts;

public enum KnownHostMutationOutcome
{
    Success,
    NotFound,
    NameConflict,
    AddressFamilyConflict,
    InvalidAddress,
}
