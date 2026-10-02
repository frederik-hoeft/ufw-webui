using Ufw.Shared.Ipc.Model;

namespace Ufw.Systemd.Security.Intent;

internal abstract record IntentPayloadBindingResult<TPayload> where TPayload : class
{
    private IntentPayloadBindingResult()
    {
    }

    internal sealed record Accepted : IntentPayloadBindingResult<TPayload>
    {
        public Accepted(TPayload payload, byte[] canonical)
        {
            ArgumentNullException.ThrowIfNull(payload);
            ArgumentNullException.ThrowIfNull(canonical);
            Payload = payload;
            Canonical = canonical;
        }

        public TPayload Payload { get; }

        public byte[] Canonical { get; }
    }

    internal sealed record Rejected : IntentPayloadBindingResult<TPayload>
    {
        public Rejected(IResponsePayload response)
        {
            ArgumentNullException.ThrowIfNull(response);
            Response = response;
        }

        public IResponsePayload Response { get; }
    }
}
