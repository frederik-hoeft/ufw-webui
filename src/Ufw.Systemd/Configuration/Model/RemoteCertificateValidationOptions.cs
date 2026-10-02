namespace Ufw.Systemd.Configuration.Model;

internal sealed class RemoteCertificateValidationOptions : IRequireValidation
{
    public required string RequiredIssuer { get; init; }

    public required string RequiredSubject { get; init; }

    public void ThrowIfInvalid()
    {
        if (string.IsNullOrWhiteSpace(RequiredIssuer) || string.IsNullOrWhiteSpace(RequiredSubject))
        {
            throw new InvalidOperationException("Remote certificate issuer and subject requirements must be non-empty.");
        }
    }
}
