using System.Collections.Frozen;
using Ufw.Ipc.Shared.Model;
using Ufw.Ipc.Shared.Serialization;
using Ufw.Roslyn.Controllers.Mapping;
using Ufw.Roslyn.Controllers.Mapping.Attributes;
using Ufw.Systemd.Api.Controllers;
using Ufw.Systemd.Api.Endpoints;
using Ufw.Systemd.Api.Framework;

namespace Ufw.Systemd.Api;

[ApiControllerRegistration<RulesController>]
[ApiControllerMappingGenerator<UfwApiEndpointMappingFactory, IRequestMessage, IResponseMessage>]
internal sealed partial class UfwApiEndpointMap : ApiEndpointMap<IRequestMessage, IResponseMessage>
{
    private static readonly NotFoundEndpoint s_notFound = new();
    private static readonly UnsupportedMethodEndpoint s_unsupportedMethod = new();

    protected override FrozenSet<string> SupportedMethods { get; } = RequestMethod.GetNames().ToFrozenSet();

    public override IApiEndpoint<IRequestMessage, IResponseMessage> GetNotFoundEndpoint() => s_notFound;

    public override IApiEndpoint<IRequestMessage, IResponseMessage> GetUnsupportedMethodEndpoint() => s_unsupportedMethod;
}
