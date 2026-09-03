namespace Ufw.Systemd.Security.Intent;

internal interface INonceStore
{
    /// <summary>
    /// Records <paramref name="nonce"/> until <paramref name="expiresAtUnix"/>.
    /// Returns <see langword="false"/> if the nonce was already consumed.
    /// </summary>
    ValueTask<bool> TryConsumeAsync(string nonce, long expiresAtUnix, CancellationToken cancellationToken);
}
