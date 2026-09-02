namespace Ufw.Ipc.Shared.Model.Responses.Domain;

public sealed record IntentContextResponse(int ProtocolVersion, string DeploymentId) : OkResponseBase;
