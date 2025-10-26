using System.Net;

namespace Ufw.Ipc.Shared.Model.Responses;

public abstract record OkResponseBase() : ResponseMessage(HttpStatusCode.OK);
