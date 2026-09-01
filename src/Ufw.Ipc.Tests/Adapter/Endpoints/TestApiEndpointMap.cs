using System.Collections.Frozen;
using Ufw.Ipc.Shared.Model;
using Ufw.Ipc.Shared.Serialization;
using Ufw.Roslyn.Controllers.Mapping;
using Ufw.Systemd.Api.Endpoints;

namespace Ufw.Ipc.Tests.Adapter.Endpoints;

/// <summary>
/// Programmatic endpoint map for tests. Uses the production routing tree without source-generated controllers.
/// </summary>
internal sealed class TestApiEndpointMap : ApiEndpointMap<IRequestMessage, IResponseMessage>
{
    private static readonly NotFoundEndpoint s_notFound = new();
    private static readonly UnsupportedMethodEndpoint s_unsupportedMethod = new();

    private readonly ApiEndpointMapping<IRequestMessage, IResponseMessage>[] _mappings;

    public TestApiEndpointMap(IEnumerable<ApiEndpointMapping<IRequestMessage, IResponseMessage>> mappings)
    {
        ArgumentNullException.ThrowIfNull(mappings);
        _mappings = [.. mappings];
    }

    protected override FrozenSet<string> SupportedMethods { get; } = RequestMethod.GetNames().ToFrozenSet();

    protected override ApiEndpointMapping<IRequestMessage, IResponseMessage>[] GetMappings() => _mappings;

    public override IApiEndpoint<IRequestMessage, IResponseMessage> GetNotFoundEndpoint() => s_notFound;

    public override IApiEndpoint<IRequestMessage, IResponseMessage> GetUnsupportedMethodEndpoint() => s_unsupportedMethod;
}
