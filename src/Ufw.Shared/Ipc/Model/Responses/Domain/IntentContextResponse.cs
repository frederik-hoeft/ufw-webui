namespace Ufw.Shared.Ipc.Model.Responses.Domain;

public sealed record IntentContextResponse(int ProtocolVersion, string DeploymentId) : OkResponseBase;
