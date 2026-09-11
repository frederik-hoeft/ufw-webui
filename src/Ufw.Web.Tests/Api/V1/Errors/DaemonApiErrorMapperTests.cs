using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Ufw.Ipc.Client;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Web.Api.V1.Errors;

namespace Ufw.Web.Tests.Api.V1.Errors;

[TestClass]
public sealed class DaemonApiErrorMapperTests
{
    [TestMethod]
    public void MapProxyFailure_ValidationErrorsProducesBadRequestValidationProblem()
    {
        UfwIpcException exception = new(
            StatusCodes.Status422UnprocessableEntity,
            "Invalid rule.",
            [
                new ModelValidationError("Payload.Rule.Action", "Action is invalid."),
                new ModelValidationError("Payload.Rule.Protocol", "Protocol is invalid."),
            ]);
        DaemonApiErrorMapper mapper = new();

        DaemonApiError error = mapper.MapProxyFailure(exception);

        Assert.AreEqual(StatusCodes.Status400BadRequest, error.StatusCode);
        ValidationProblemDetails problem = Assert.IsInstanceOfType<ValidationProblemDetails>(error.Problem);
        Assert.AreEqual(StatusCodes.Status400BadRequest, problem.Status);
        Assert.AreEqual("Invalid rule.", problem.Title);
        CollectionAssert.AreEqual(new[] { "Action is invalid." }, problem.Errors["Payload.Rule.Action"]);
        CollectionAssert.AreEqual(new[] { "Protocol is invalid." }, problem.Errors["Payload.Rule.Protocol"]);
    }

    [TestMethod]
    public void MapProxyFailure_ValidHttpStatusPreservesStatusAndDetail()
    {
        DaemonApiErrorMapper mapper = new();

        DaemonApiError error = mapper.MapProxyFailure(new UfwIpcException(StatusCodes.Status409Conflict, "Rule already exists."));

        Assert.AreEqual(StatusCodes.Status409Conflict, error.StatusCode);
        Assert.AreEqual(StatusCodes.Status409Conflict, error.Problem.Status);
        Assert.AreEqual("Rule already exists.", error.Problem.Detail);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(399)]
    [DataRow(600)]
    public void MapProxyFailure_InvalidHttpStatusFallsBackToBadGateway(int daemonStatusCode)
    {
        DaemonApiErrorMapper mapper = new();

        DaemonApiError error = mapper.MapProxyFailure(new UfwIpcException(daemonStatusCode, "Unexpected daemon status."));

        Assert.AreEqual(StatusCodes.Status502BadGateway, error.StatusCode);
        Assert.AreEqual(StatusCodes.Status502BadGateway, error.Problem.Status);
        Assert.AreEqual("Unexpected daemon status.", error.Problem.Detail);
    }

    [TestMethod]
    public void MapUnavailable_AlwaysProducesBadGateway()
    {
        DaemonApiErrorMapper mapper = new();

        DaemonApiError error = mapper.MapUnavailable(new UfwIpcException(StatusCodes.Status400BadRequest, "transport failed"));

        Assert.AreEqual(StatusCodes.Status502BadGateway, error.StatusCode);
        Assert.AreEqual("transport failed", error.Problem.Detail);
    }

    [TestMethod]
    public void MapInvalidResponse_UsesValidationFailureAsBadGatewayDetail()
    {
        DaemonApiErrorMapper mapper = new();

        DaemonApiError error = mapper.MapInvalidResponse(new InvalidDataException("malformed daemon inventory"));

        Assert.AreEqual(StatusCodes.Status502BadGateway, error.StatusCode);
        Assert.AreEqual("malformed daemon inventory", error.Problem.Detail);
    }
}
