namespace Ufw.Shared.Ipc.Model.Responses.Domain;

public sealed record NetworkInterfaceListResponse(IReadOnlyList<string> Interfaces) : OkResponseBase;
