namespace Ufw.Roslyn.Controllers.Mapping;

public interface IApiEndpointMap<in TRequestEnvelope, TResponseEnvelope>
{
    IApiEndpoint<TRequestEnvelope, TResponseEnvelope> GetNotFoundEndpoint();

    IApiEndpoint<TRequestEnvelope, TResponseEnvelope> GetUnsupportedMethodEndpoint();

    IApiEndpoint<TRequestEnvelope, TResponseEnvelope> Match(string method, string route);
}
