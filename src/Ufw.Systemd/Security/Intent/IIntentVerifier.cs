using Ufw.Ipc.Shared.Security.Intent;

namespace Ufw.Systemd.Security.Intent;

internal interface IIntentVerifier
{
    IntentVerificationResult VerifyAdd(ISignedIntent intent);

    IntentVerificationResult VerifyDelete(ISignedIntent intent);
}
