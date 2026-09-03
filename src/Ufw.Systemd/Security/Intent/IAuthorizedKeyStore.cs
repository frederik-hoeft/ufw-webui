using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace Ufw.Systemd.Security.Intent;

internal interface IAuthorizedKeyStore
{
    bool TryGetKey(string keyId, [NotNullWhen(true)] out ECDsa? key);
}
