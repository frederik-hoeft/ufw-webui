using Ufw.Shared.Ipc.Serialization.Json;
using Ufw.Shared.Security.Intent;
using Ufw.Systemd.Configuration;
using Ufw.Systemd.Security.Intent;

namespace Ufw.Systemd.Tests.TestSupport;

internal static class IntentVerifierTestFactory
{
    public static IntentVerifier Create(
        IAuthorizedKeyStore authorizedKeys,
        IDeploymentIdentityProvider deploymentIdentity,
        IConfiguration configuration,
        TimeProvider timeProvider)
    {
        IIntentEnvelopeVerifier envelopeVerifier = new IntentEnvelopeVerifier(authorizedKeys, deploymentIdentity, configuration, timeProvider);
        return new IntentVerifier(
            envelopeVerifier,
            new AddIntentPayloadBinder(MessageJsonSerializerContext.Default),
            new DeleteIntentPayloadBinder(MessageJsonSerializerContext.Default),
            new BatchDeleteIntentPayloadBinder(MessageJsonSerializerContext.Default),
            new InsertIntentPayloadBinder(MessageJsonSerializerContext.Default),
            new ReorderIntentPayloadBinder(MessageJsonSerializerContext.Default),
            new ReplaceIntentPayloadBinder(MessageJsonSerializerContext.Default));
    }
}
