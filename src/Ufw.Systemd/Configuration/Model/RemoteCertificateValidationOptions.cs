namespace Ufw.Systemd.Configuration.Model;

internal sealed class RemoteCertificateValidationOptions : IRequireValidation
{
    public required string RequiredIssuer { get; set; }

    public required string RequiredSubject { get; set; }

    public bool AssertIsValid() => this is
    {
        RequiredIssuer.Length: > 0,
        RequiredSubject.Length: > 0,
    } ? true : throw new InvalidOperationException($"invalid {nameof(RemoteCertificateValidationOptions)}");
}
