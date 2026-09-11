using Microsoft.Extensions.Logging;
using Ufw.Web.Services.ErrorHandling;
using Wkg.AspNetCore.Exceptions;

namespace Ufw.Web.Tests.Services.ErrorHandling;

[TestClass]
public sealed class ApplicationErrorSentryTests
{
    [TestMethod]
    public void Watch_UnhandledFailureIsLoggedBeforeWkgTransformsIt()
    {
        RecordingLogger<ApplicationErrorSentry> logger = new();
        ApplicationErrorSentry sentry = new(logger);
        InvalidOperationException failure = new("boom");

        Assert.Throws<ApiProxyException>(() => sentry.Watch<int>(() => throw failure));

        Assert.IsTrue(logger.Entries.Any(entry => entry.Level == LogLevel.Error && ReferenceEquals(entry.Exception, failure)));
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, exception));
    }
}
