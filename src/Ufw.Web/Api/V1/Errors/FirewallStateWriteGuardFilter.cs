using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Api.V1.Controllers;
using Ufw.Web.Services.Daemon;
using Ufw.Web.Services.Rules;

namespace Ufw.Web.Api.V1.Errors;

/// <summary>
/// Fails closed on application-owned writes as well as signed firewall writes when the observed firewall model is ambiguous.
/// Authentication endpoints remain available for session recovery.
/// </summary>
internal sealed class FirewallStateWriteGuardFilter(IRuleDaemonGateway daemonRules) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (context.Controller is AuthController || !IsWrite(context.HttpContext.Request.Method))
        {
            _ = await next();
            return;
        }

        DaemonResult<RuleListResponse> result = await daemonRules.GetRulesAsync(context.HttpContext.RequestAborted);
        if (!result.TryGetResult(out RuleListResponse? snapshot, out _))
        {
            context.Result = Problem(StatusCodes.Status503ServiceUnavailable,
                "Firewall state unavailable", "The authoritative firewall state could not be verified before the requested change.");
            return;
        }

        // The current daemon supplies the assessment, but recompute from the source identities at this boundary as defense in depth.
        FirewallStateAssessment assessment = FirewallStateAssessmentEvaluator.Evaluate(snapshot.Rules);
        if (!assessment.IsClean)
        {
            context.Result = Problem(StatusCodes.Status409Conflict,
                "Firewall state requires manual repair", "Duplicate semantic firewall rules were detected. All management edits are blocked until the state is repaired directly with UFW.");
            return;
        }

        _ = await next();
    }

    private static bool IsWrite(string method) => HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);

    private static ObjectResult Problem(int status, string title, string detail) => new(ApiProblemDetailsFactory.Create(status, title, detail))
    {
        StatusCode = status,
        ContentTypes = { "application/problem+json" },
    };
}
