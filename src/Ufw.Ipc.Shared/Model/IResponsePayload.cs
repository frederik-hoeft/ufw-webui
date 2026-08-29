using System.Net;
using Ufw.Roslyn.Controllers;

namespace Ufw.Ipc.Shared.Model;

public interface IResponsePayload : IMessagePayload, IIdentifiable
{
    internal HttpStatusCode StatusCode { get; }
}
