namespace Ufw.Web.Tests.Integration.Support;

internal sealed class IntegrationTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow = new(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void SetUtcNow(DateTimeOffset utcNow) => _utcNow = utcNow;
}
