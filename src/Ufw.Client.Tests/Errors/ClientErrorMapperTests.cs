using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using Ufw.Client.Api;
using Ufw.Client.Errors;
using Ufw.Client.Localization;
using Ufw.Client.Tests.Support;

namespace Ufw.Client.Tests.Errors;

[TestClass]
public sealed class ClientErrorMapperTests
{
    private readonly ClientErrorMapper _mapper = new(NullLogger<ClientErrorMapper>.Instance, new PassthroughStringLocalizer<ErrorsStrings>());

    [TestMethod]
    [DataRow(HttpStatusCode.Unauthorized, ClientErrorKind.Unauthorized, false)]
    [DataRow(HttpStatusCode.Forbidden, ClientErrorKind.Forbidden, false)]
    [DataRow(HttpStatusCode.NotFound, ClientErrorKind.RequestRejected, true)]
    [DataRow(HttpStatusCode.RequestTimeout, ClientErrorKind.Unavailable, true)]
    [DataRow(HttpStatusCode.TooManyRequests, ClientErrorKind.Unavailable, true)]
    [DataRow(HttpStatusCode.InternalServerError, ClientErrorKind.Unavailable, true)]
    public void Describe_ApiStatusMapsToStableClientSemantics(HttpStatusCode status, ClientErrorKind kind, bool retryable)
    {
        ClientError error = _mapper.Describe(new ApiRequestException(status, "server message"));

        Assert.AreEqual(kind, error.Kind);
        Assert.AreEqual(retryable, error.Retryable);
    }

    [TestMethod]
    public void Describe_ConflictAndValidationPreserveServerMessage()
    {
        ClientError conflict = _mapper.Describe(new ApiRequestException(HttpStatusCode.Conflict, "conflict detail"));
        ClientError invalid = _mapper.Describe(new ApiRequestException(HttpStatusCode.UnprocessableEntity, "validation detail"));

        Assert.AreEqual("conflict detail", conflict.Message);
        Assert.AreEqual("validation detail", invalid.Message);
    }

    [TestMethod]
    public void TryDescribe_UnknownExceptionReturnsFalseWithoutManufacturingError()
    {
        bool known = _mapper.TryDescribe(new InvalidOperationException("unexpected"), out ClientError error);

        Assert.IsFalse(known);
        Assert.IsNull(error);
    }

    [TestMethod]
    public void Describe_UnknownExceptionGetsStableDiagnosticReferencePerException()
    {
        InvalidOperationException exception = new("unexpected");

        ClientError first = _mapper.Describe(exception);
        ClientError second = _mapper.Describe(exception);

        Assert.AreEqual(ClientErrorKind.Unexpected, first.Kind);
        Assert.IsNotNull(first.DiagnosticReference);
        Assert.AreEqual(12, first.DiagnosticReference.Length);
        Assert.AreEqual(first.DiagnosticReference, second.DiagnosticReference);
    }

    [TestMethod]
    public void Describe_TransportCancellationProtocolAndArgumentFailuresUseExpectedRetryPolicy()
    {
        AssertError(new HttpRequestException(), ClientErrorKind.Unavailable, true);
        AssertError(new OperationCanceledException(), ClientErrorKind.Canceled, true);
        AssertError(new ApiProtocolException("bad protocol"), ClientErrorKind.Protocol, false);
        AssertError(new ArgumentException("bad request"), ClientErrorKind.RequestRejected, false);
    }

    private void AssertError(Exception exception, ClientErrorKind kind, bool retryable)
    {
        ClientError error = _mapper.Describe(exception);
        Assert.AreEqual(kind, error.Kind);
        Assert.AreEqual(retryable, error.Retryable);
    }
}
