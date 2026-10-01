namespace Ufw.Systemd.Configuration.Model;

internal sealed class PipeOptions : IRequireValidation
{
    public required string PipeName { get; init; }

    public void ThrowIfInvalid()
    {
        if (string.IsNullOrWhiteSpace(PipeName))
        {
            throw new InvalidOperationException("A pipe endpoint is required.");
        }
    }
}
