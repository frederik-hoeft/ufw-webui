using Ufw.Roslyn.Controllers;

namespace Ufw.Ipc.Shared.Model.Requests;

public abstract record RequestMessage : IMessagePayload, IIdentifiable
{
    private readonly string _method;
    private readonly string _id;

    protected RequestMessage(string method, string route)
    {
        ArgumentException.ThrowIfNullOrEmpty(method, nameof(method));
        ArgumentException.ThrowIfNullOrEmpty(route, nameof(route));
        _method = method;
        _id = route;
    }

    protected RequestMessage(RequestMethod method, string route)
    {
        if (!RequestMethod.IsDefined(method))
        {
            throw new ArgumentOutOfRangeException(nameof(method), method, $"The specified request method is not valid. Must be one of {string.Join(", ", RequestMethod.GetValues())}.");
        }

        ArgumentException.ThrowIfNullOrEmpty(route, nameof(route));
        _method = method.ToString();
        _id = route;
    }

    string IIdentifiable.Id => _id;

    string IIdentifiable.Method => _method;
}
