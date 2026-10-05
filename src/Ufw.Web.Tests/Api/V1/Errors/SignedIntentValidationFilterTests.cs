using System.Buffers.Text;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Serialization.Json;
using Ufw.Shared.Security.Intent;
using Ufw.Web.Api.V1.Errors;
using Ufw.Web.Model.V1.Errors;
using Ufw.Web.Model.V1.Rules.Intent;

namespace Ufw.Web.Tests.Api.V1.Errors;

[TestClass]
public sealed class SignedIntentValidationFilterTests
{
    [TestMethod]
    public void OnActionExecuting_SemanticRuleFailurePreservesStableValidationCode()
    {
        FirewallRuleSpecification rule = new()
        {
            Action = FirewallAction.Allow,
            AddressFamily = FirewallAddressFamily.IPv4,
            Direction = FirewallDirection.In,
            Protocol = FirewallProtocol.Tcp,
            Source = RuleSpecificationNormalizer.ANY,
            Destination = RuleSpecificationNormalizer.ANY,
            DestinationPorts = "0",
        };
        AddRuleIntentRequest request = new()
        {
            Version = IntentProtocol.VERSION,
            DeploymentId = Base64Url.EncodeToString(new byte[IntentProtocol.DEPLOYMENT_ID_SIZE_BYTES]),
            KeyId = IntentProtocol.KEY_ID_PREFIX + Base64Url.EncodeToString(new byte[SHA256.HashSizeInBytes]),
            IssuedAtUnix = 1_700_000_000,
            Nonce = Base64Url.EncodeToString(new byte[IntentProtocol.NONCE_SIZE_BYTES]),
            Operation = IntentOperations.ADD_RULE,
            Payload = JsonSerializer.SerializeToElement(new AddRulePayload { Rule = rule }, MessageJsonSerializerContext.Default.AddRulePayload),
            Signature = Base64Url.EncodeToString(new byte[64]),
        };

        List<ValidationResult> validationResults = [];
        bool valid = Validator.TryValidateObject(request, new ValidationContext(request), validationResults, validateAllProperties: true);
        Assert.IsFalse(valid);

        ModelStateDictionary modelState = new();
        foreach (ValidationResult validationResult in validationResults)
        {
            foreach (string memberName in validationResult.MemberNames.DefaultIfEmpty(string.Empty))
            {
                modelState.AddModelError(memberName, validationResult.ErrorMessage ?? string.Empty);
            }
        }

        ActionContext actionContext = new(new DefaultHttpContext(), new RouteData(), new ActionDescriptor(), modelState);
        ActionExecutingContext context = new(actionContext, [], new Dictionary<string, object?> { ["request"] = request }, new object());
        SignedIntentValidationFilter filter = new();

        filter.OnActionExecuting(context);

        BadRequestObjectResult result = Assert.IsInstanceOfType<BadRequestObjectResult>(context.Result);
        ProblemDetails problem = Assert.IsInstanceOfType<ProblemDetails>(result.Value);
        IReadOnlyList<ApiValidationError> errors = Assert.IsInstanceOfType<IReadOnlyList<ApiValidationError>>(
            problem.Extensions[ApiProblemDetails.VALIDATION_ERRORS_PROPERTY]);
        ApiValidationError error = errors.Single(candidate => candidate.PropertyName == "Payload.Rule.DestinationPorts");
        Assert.AreEqual(FirewallRuleValidationErrorCodes.PORTS_SYNTAX_INVALID, error.Code);
    }
}
