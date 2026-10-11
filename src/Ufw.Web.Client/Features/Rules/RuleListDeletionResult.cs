using Ufw.Web.Client.Features.Rules.Metadata;
using Ufw.Web.Client.Services.Errors;

namespace Ufw.Web.Client.Features.Rules;

internal sealed record RuleListDeletionResult(RuleGroupCleanupResult? GroupCleanup, ClientError? GroupCleanupError);
