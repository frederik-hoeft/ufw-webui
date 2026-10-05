using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Ufw.Ipc.Client;
using Ufw.Web.Api.V1.Errors;
using Ufw.Web.Services.Daemon;

namespace Ufw.Web.Tests.Api.V1.Errors;

[TestClass]
public sealed class DaemonApiExceptionFilterTests
{
    [TestMethod]
    public void OnException_RawDaemonFailureUsesProxyMapping()
    {
        DaemonApiExceptionFilter filter = new(new DaemonApiErrorMapper());
        ExceptionContext context = CreateContext(new UfwIpcException(StatusCodes.Status409Conflict, "conflict"));

        filter.OnException(context);

        Assert.IsTrue(context.ExceptionHandled);
        ObjectResult result = Assert.IsInstanceOfType<ObjectResult>(context.Result);
        Assert.AreEqual(StatusCodes.Status409Conflict, result.StatusCode);
        ProblemDetails problem = Assert.IsInstanceOfType<ProblemDetails>(result.Value);
        Assert.AreEqual("conflict", problem.Detail);
    }

    [TestMethod]
    public void OnException_WorkflowClassifiedUnavailableFailureForcesBadGateway()
    {
        DaemonApiExceptionFilter filter = new(new DaemonApiErrorMapper());
        UfwIpcError daemonError = new(StatusCodes.Status400BadRequest, "enumeration failed");
        ExceptionContext context = CreateContext(new DaemonUnavailableException(daemonError));

        filter.OnException(context);

        Assert.IsTrue(context.ExceptionHandled);
        ObjectResult result = Assert.IsInstanceOfType<ObjectResult>(context.Result);
        Assert.AreEqual(StatusCodes.Status502BadGateway, result.StatusCode);
        ProblemDetails problem = Assert.IsInstanceOfType<ProblemDetails>(result.Value);
        Assert.AreEqual("enumeration failed", problem.Detail);
    }

    [TestMethod]
    public void OnException_InvalidDaemonResponseForcesBadGateway()
    {
        DaemonApiExceptionFilter filter = new(new DaemonApiErrorMapper());
        ExceptionContext context = CreateContext(new DaemonInvalidResponseException("malformed inventory"));

        filter.OnException(context);

        Assert.IsTrue(context.ExceptionHandled);
        ObjectResult result = Assert.IsInstanceOfType<ObjectResult>(context.Result);
        Assert.AreEqual(StatusCodes.Status502BadGateway, result.StatusCode);
        ProblemDetails problem = Assert.IsInstanceOfType<ProblemDetails>(result.Value);
        Assert.AreEqual("malformed inventory", problem.Detail);
    }

    [TestMethod]
    public void OnException_UnrelatedFailureRemainsUnhandled()
    {
        DaemonApiExceptionFilter filter = new(new DaemonApiErrorMapper());
        InvalidOperationException expected = new("application failure");
        ExceptionContext context = CreateContext(expected);

        filter.OnException(context);

        Assert.IsFalse(context.ExceptionHandled);
        Assert.IsNull(context.Result);
        Assert.AreSame(expected, context.Exception);
    }

    private static ExceptionContext CreateContext(Exception exception)
    {
        DefaultHttpContext httpContext = new();
        ActionContext actionContext = new(httpContext, new RouteData(), new ActionDescriptor());
        return new ExceptionContext(actionContext, []) { Exception = exception };
    }
}
