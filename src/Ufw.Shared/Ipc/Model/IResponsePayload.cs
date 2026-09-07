using System.Net;
using Ufw.Roslyn.Controllers;

namespace Ufw.Shared.Ipc.Model;

public interface IResponsePayload : IMessagePayload, IIdentifiable
{
    internal HttpStatusCode StatusCode { get; }
}
