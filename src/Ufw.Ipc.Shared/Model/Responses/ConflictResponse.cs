using System.Net;

namespace Ufw.Ipc.Shared.Model.Responses;

public sealed record ConflictResponse(string? Message = null) : ErrorResponse(HttpStatusCode.Conflict, Message);
