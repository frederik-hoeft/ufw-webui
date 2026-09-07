using System.Net;

namespace Ufw.Shared.Ipc.Model.Responses;

public sealed record ConflictResponse(string? Message = null) : ErrorResponse(HttpStatusCode.Conflict, Message);
