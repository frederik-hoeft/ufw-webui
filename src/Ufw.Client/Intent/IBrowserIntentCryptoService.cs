namespace Ufw.Client.Intent;

internal interface IBrowserIntentCryptoService
{
    Task<string> GetKeyIdAsync(string privateKey, CancellationToken cancellationToken = default);

    Task<string> CreateNonceAsync(int sizeBytes, CancellationToken cancellationToken = default);

    Task<string> SignAsync(string privateKey, byte[] payload, CancellationToken cancellationToken = default);
}
