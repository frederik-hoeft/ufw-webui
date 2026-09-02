namespace Ufw.Ipc.Shared.Security.Intent;

public static class IntentProtocol
{
    public const int VERSION = 1;
    public const string CONTEXT = "ufw-intent/1";
    public const string KEY_ID_PREFIX = "sha256:";
    public const int NONCE_SIZE_BYTES = 16;
    public const int MINIMUM_NONCE_SIZE_BYTES = 16;
}
