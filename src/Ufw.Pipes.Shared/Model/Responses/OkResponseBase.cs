using System.Net;

namespace Ufw.Pipes.Shared.Model.Responses;

public abstract record OkResponseBase() : ResponseMessage(HttpStatusCode.OK);
