using System.Net;

namespace Ufw.Shared.Ipc.Model.Responses;

public abstract record OkResponseBase() : ResponseMessage(HttpStatusCode.OK);
