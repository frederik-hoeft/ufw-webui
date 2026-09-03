using Ufw.Ipc.Shared.Model;
using Ufw.Systemd.Api;
using Ufw.Systemd.Api.Endpoints;

namespace Ufw.Systemd.Tests.Api;

[TestClass]
public sealed class UfwApiEndpointMapTests
{
    [TestMethod]
    public void Match_ResolvesEndpointsFromMultipleRegisteredControllers()
    {
        UfwApiEndpointMap endpointMap = new();

        Assert.IsNotInstanceOfType<NotFoundEndpoint>(endpointMap.Match(RequestMethod.Get.ToString(), "/api/v1/intent/context"));
        Assert.IsNotInstanceOfType<NotFoundEndpoint>(endpointMap.Match(RequestMethod.Get.ToString(), "/api/v1/rules"));
    }
}
