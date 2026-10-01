namespace Ufw.Systemd.Configuration.Model;

internal sealed class SecurityOptions : IRequireValidation
{
    public required string AuthorizedKeysPath { get; init; }

    public required string NonceStorePath { get; init; }

    public required string DeploymentIdPath { get; init; }

    public required string ReorderRecoveryJournalPath { get; init; }

    public required TimeSpan MaxIntentAge { get; init; }

    public required TimeSpan ClockSkew { get; init; }

    public void ThrowIfInvalid()
    {
        if (string.IsNullOrWhiteSpace(AuthorizedKeysPath)
            || string.IsNullOrWhiteSpace(NonceStorePath)
            || string.IsNullOrWhiteSpace(DeploymentIdPath)
            || string.IsNullOrWhiteSpace(ReorderRecoveryJournalPath)
            || MaxIntentAge <= TimeSpan.Zero
            || ClockSkew < TimeSpan.Zero)
        {
            throw new InvalidOperationException("Security file paths and intent timing policy must be valid.");
        }
    }
}
