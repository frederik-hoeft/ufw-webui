using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Moq;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Api.V1.Errors;
using Ufw.Web.Services.Daemon;
using Ufw.Web.Services.Rules;

namespace Ufw.Web.Tests.Api.V1.Errors;

[TestClass]
public sealed class FirewallStateWriteGuardFilterTests
{
    [TestMethod]
    public async Task Post_WhenDuplicateIdentityExists_ReturnsConflictWithoutExecutingControllerAsync()
    {
        Mock<IRuleDaemonGateway> daemon = new(MockBehavior.Strict);
        daemon.Setup(x => x.GetRulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DaemonResult.Success(Snapshot(["sha256:duplicate", "sha256:other", "sha256:duplicate"])));
        FirewallStateWriteGuardFilter filter = new(daemon.Object);
        (ActionExecutingContext context, ActionExecutionDelegate next, Func<bool> executed) = CreateAction("POST");

        await filter.OnActionExecutionAsync(context, next);

        Assert.IsFalse(executed());
        ObjectResult result = Assert.IsInstanceOfType<ObjectResult>(context.Result);
        Assert.AreEqual(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.AreEqual("application/problem+json", result.ContentTypes.Single());
    }

    [TestMethod]
    public async Task Post_WhenCleanSnapshot_AllowsControllerAsync()
    {
        Mock<IRuleDaemonGateway> daemon = new(MockBehavior.Strict);
        daemon.Setup(x => x.GetRulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DaemonResult.Success(Snapshot(["sha256:first", "sha256:second"])));
        FirewallStateWriteGuardFilter filter = new(daemon.Object);
        (ActionExecutingContext context, ActionExecutionDelegate next, Func<bool> executed) = CreateAction("POST");

        await filter.OnActionExecutionAsync(context, next);

        Assert.IsTrue(executed());
        Assert.IsNull(context.Result);
    }

    [TestMethod]
    public async Task Post_WhenDaemonCannotReadSnapshot_ReturnsUnavailableWithoutExecutingControllerAsync()
    {
        Mock<IRuleDaemonGateway> daemon = new(MockBehavior.Strict);
        daemon.Setup(x => x.GetRulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DaemonResult.Failure<RuleListResponse>(new Ufw.Ipc.Client.UfwIpcError(502, "daemon unavailable")));
        FirewallStateWriteGuardFilter filter = new(daemon.Object);
        (ActionExecutingContext context, ActionExecutionDelegate next, Func<bool> executed) = CreateAction("POST");

        await filter.OnActionExecutionAsync(context, next);

        Assert.IsFalse(executed());
        ObjectResult result = Assert.IsInstanceOfType<ObjectResult>(context.Result);
        Assert.AreEqual(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
    }

    [TestMethod]
    public async Task Get_DoesNotRequireHealthyOrCleanFirewallAsync()
    {
        Mock<IRuleDaemonGateway> daemon = new(MockBehavior.Strict);
        FirewallStateWriteGuardFilter filter = new(daemon.Object);
        (ActionExecutingContext context, ActionExecutionDelegate next, Func<bool> executed) = CreateAction("GET");

        await filter.OnActionExecutionAsync(context, next);

        Assert.IsTrue(executed());
        daemon.VerifyNoOtherCalls();
    }

    private static RuleListResponse Snapshot(IReadOnlyList<string> ids) => new(
        true,
        [.. ids.Select(static id => new ListedFirewallRule { RuleId = id })],
        new FirewallConfigurationSnapshot(true, FirewallDefaultPolicy.Deny, FirewallDefaultPolicy.Allow, FirewallDefaultPolicy.Deny));

    private static (ActionExecutingContext Context, ActionExecutionDelegate Next, Func<bool> Executed) CreateAction(string method)
    {
        DefaultHttpContext http = new();
        http.Request.Method = method;
        ActionContext action = new(http, new RouteData(), new ActionDescriptor());
        ActionExecutingContext context = new(action, [], new Dictionary<string, object?>(), new object());
        bool executed = false;
        ActionExecutionDelegate next = () =>
        {
            executed = true;
            return Task.FromResult(new ActionExecutedContext(action, [], context.Controller));
        };
        return (context, next, () => executed);
    }
}
