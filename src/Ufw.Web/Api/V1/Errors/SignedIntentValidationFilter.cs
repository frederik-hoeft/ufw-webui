using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Ufw.Web.Model.V1.Errors;
using Ufw.Web.Model.V1.Rules.Intent;

namespace Ufw.Web.Api.V1.Errors;

internal sealed class SignedIntentValidationFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.ModelState.IsValid)
        {
            return;
        }

        List<ApiValidationError> structuredErrors = context.ActionArguments.Values
            .OfType<SignedRuleIntentRequest>()
            .SelectMany(static request => request.GetApiValidationErrors())
            .Where(static error => error.Code is not null)
            .ToList();
        if (structuredErrors.Count == 0)
        {
            return;
        }

        ProblemDetails problem = ApiProblemDetailsFactory.CreateValidation(context.ModelState, structuredErrors);
        context.Result = new BadRequestObjectResult(problem);
    }

    public void OnActionExecuted(ActionExecutedContext context) => ArgumentNullException.ThrowIfNull(context);
}
