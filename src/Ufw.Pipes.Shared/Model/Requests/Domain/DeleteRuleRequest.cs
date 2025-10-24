namespace Ufw.Pipes.Shared.Model.Requests.Domain;

public sealed record DeleteRuleRequest(string RuleId) : RequestMessage(RequestMethod.Delete, "/api/v1/rules");