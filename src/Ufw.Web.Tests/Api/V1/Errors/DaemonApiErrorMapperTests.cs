using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Ufw.Ipc.Client;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Web.Api.V1.Errors;
using Ufw.Web.Model.V1.Errors;
using Ufw.Web.Services.Daemon;

namespace Ufw.Web.Tests.Api.V1.Errors;

[TestClass]
public sealed class DaemonApiErrorMapperTests
{
    [TestMethod]
    public void MapProxyFailure_ValidationErrorsPreserveOrderDuplicatesAndStableCodes()
    {
        UfwIpcError daemonError = new(
            StatusCodes.Status422UnprocessableEntity,
            "Invalid rule.",
            [
                new ModelValidationError("Payload.Rule.Source", "Address is invalid.", FirewallRuleValidationErrorCodes.ADDRESS_INVALID),
                new ModelValidationError("Payload.Rule.Source", "Address family does not match.", FirewallRuleValidationErrorCodes.ADDRESS_FAMILY_MISMATCH),
                new ModelValidationError("Payload.Rule.Protocol", "Protocol is invalid.", FirewallRuleValidationErrorCodes.PROTOCOL_UNSUPPORTED),
            ]);
        DaemonApiErrorMapper mapper = new();

        DaemonApiError error = mapper.MapProxyFailure(daemonError);

        Assert.AreEqual(StatusCodes.Status400BadRequest, error.StatusCode);
        ProblemDetails problem = error.Problem;
        Assert.AreEqual(StatusCodes.Status400BadRequest, problem.Status);
        Assert.AreEqual("Invalid rule.", problem.Title);
        IReadOnlyList<ApiValidationError> validationErrors = Assert.IsInstanceOfType<IReadOnlyList<ApiValidationError>>(
            problem.Extensions[ApiProblemDetails.VALIDATION_ERRORS_PROPERTY]);
        Assert.HasCount(3, validationErrors);
        Assert.AreEqual(new ApiValidationError(
            "Payload.Rule.Source",
            FirewallRuleValidationErrorCodes.ADDRESS_INVALID,
            "Address is invalid."), validationErrors[0]);
        Assert.AreEqual(new ApiValidationError(
            "Payload.Rule.Source",
            FirewallRuleValidationErrorCodes.ADDRESS_FAMILY_MISMATCH,
            "Address family does not match."), validationErrors[1]);
        Assert.AreEqual(new ApiValidationError(
            "Payload.Rule.Protocol",
            FirewallRuleValidationErrorCodes.PROTOCOL_UNSUPPORTED,
            "Protocol is invalid."), validationErrors[2]);
    }

    [TestMethod]
    public void MapProxyFailure_ValidHttpStatusPreservesStatusAndDetail()
    {
        DaemonApiErrorMapper mapper = new();

        DaemonApiError error = mapper.MapProxyFailure(new UfwIpcError(StatusCodes.Status409Conflict, "Rule already exists."));

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

        DaemonApiError error = mapper.MapProxyFailure(new UfwIpcError(daemonStatusCode, "Unexpected daemon status."));

        Assert.AreEqual(StatusCodes.Status502BadGateway, error.StatusCode);
        Assert.AreEqual(StatusCodes.Status502BadGateway, error.Problem.Status);
        Assert.AreEqual("Unexpected daemon status.", error.Problem.Detail);
    }

    [TestMethod]
    public void MapUnavailable_AlwaysProducesBadGateway()
    {
        DaemonApiErrorMapper mapper = new();

        DaemonApiError error = mapper.MapUnavailable(new UfwIpcError(StatusCodes.Status400BadRequest, "transport failed"));

        Assert.AreEqual(StatusCodes.Status502BadGateway, error.StatusCode);
        Assert.AreEqual("transport failed", error.Problem.Detail);
    }

    [TestMethod]
    public void MapInvalidResponse_UsesValidationFailureAsBadGatewayDetail()
    {
        DaemonApiErrorMapper mapper = new();

        DaemonApiError error = mapper.MapInvalidResponse(new DaemonInvalidResponseException("malformed daemon inventory"));

        Assert.AreEqual(StatusCodes.Status502BadGateway, error.StatusCode);
        Assert.AreEqual("malformed daemon inventory", error.Problem.Detail);
    }
}
