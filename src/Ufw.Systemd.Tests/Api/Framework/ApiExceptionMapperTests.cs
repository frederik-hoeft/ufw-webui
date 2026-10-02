using Moq;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Systemd.Api.Framework;
using Ufw.Systemd.Services.Logging;
using Ufw.Systemd.Tests.TestSupport;

namespace Ufw.Systemd.Tests.Api.Framework;

[TestClass]
public sealed class ApiExceptionMapperTests
{
    [TestMethod]
    public void Map_DebugModeAlone_DoesNotExposeExceptionDetails()
    {
        TestConfiguration configuration = new(TestAppSettingsFactory.Create(
            debugMode: true,
            exposeRemoteExceptionDetails: false));
        ApiExceptionMapper mapper = CreateMapper(configuration);
        InvalidOperationException exception = new("sensitive failure detail");

        InternalServerErrorResponse response = mapper.Map(exception);

        Assert.AreEqual("An unexpected error occurred while processing the request.", response.Message);
        Assert.IsNotNull(response.Message);
        Assert.DoesNotContain("sensitive", response.Message);
    }

    [TestMethod]
    public void Map_ExplicitRemoteDiagnosticOptIn_ExposesExceptionDetails()
    {
        TestConfiguration configuration = new(TestAppSettingsFactory.Create(
            debugMode: false,
            exposeRemoteExceptionDetails: true));
        ApiExceptionMapper mapper = CreateMapper(configuration);
        InvalidOperationException exception = new("diagnostic failure detail");

        InternalServerErrorResponse response = mapper.Map(exception);

        Assert.IsNotNull(response.Message);
        Assert.Contains("diagnostic failure detail", response.Message);
    }

    private static ApiExceptionMapper CreateMapper(TestConfiguration configuration)
    {
        Mock<ILogger<ApiExceptionMapper>> scopedLogger = new();
        Mock<ILogger> logger = new();
        logger
            .Setup(value => value.Scoped<ApiExceptionMapper>())
            .Returns(scopedLogger.Object);
        return new ApiExceptionMapper(configuration, logger.Object);
    }
}
