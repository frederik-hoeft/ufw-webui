using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Ufw.Web.Api.V1.Errors;
using Ufw.Web.Model.V1.Errors;

namespace Ufw.Web.Tests.Api.V1.Errors;

[TestClass]
public sealed class ApiProblemDetailsFactoryTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [TestMethod]
    public void CreateValidation_ModelStatePreservesRepeatedErrorsAndOrdersPropertiesDeterministically()
    {
        ModelStateDictionary modelState = new();
        modelState.AddModelError("Payload.Rule.Source", "First source failure.");
        modelState.AddModelError("Payload.Rule.Protocol", "Protocol failure.");
        modelState.AddModelError("Payload.Rule.Source", "Second source failure.");

        ProblemDetails problem = ApiProblemDetailsFactory.CreateValidation(modelState);

        Assert.AreEqual(StatusCodes.Status400BadRequest, problem.Status);
        Assert.AreEqual("One or more validation errors occurred.", problem.Title);
        IReadOnlyList<ApiValidationError> errors = Assert.IsInstanceOfType<IReadOnlyList<ApiValidationError>>(
            problem.Extensions[ApiProblemDetails.VALIDATION_ERRORS_PROPERTY]);
        CollectionAssert.AreEqual(
            new[]
            {
                new ApiValidationError("Payload.Rule.Protocol", Code: null, "Protocol failure."),
                new ApiValidationError("Payload.Rule.Source", Code: null, "First source failure."),
                new ApiValidationError("Payload.Rule.Source", Code: null, "Second source failure."),
            },
            errors.ToArray());
    }

    [TestMethod]
    public void CreateValidation_SerializesValidationErrorsAsProblemDetailsExtension()
    {
        ProblemDetails problem = ApiProblemDetailsFactory.CreateValidation(
        [
            new ApiValidationError("Payload.Rule.Action", "firewall.rule.action.unsupported", "Action is invalid."),
        ]);

        string json = JsonSerializer.Serialize(problem, JsonOptions);
        using JsonDocument document = JsonDocument.Parse(json);

        JsonElement validationErrors = document.RootElement.GetProperty(ApiProblemDetails.VALIDATION_ERRORS_PROPERTY);
        Assert.AreEqual(JsonValueKind.Array, validationErrors.ValueKind);
        JsonElement error = validationErrors.EnumerateArray().Single();
        Assert.AreEqual("Payload.Rule.Action", error.GetProperty("propertyName").GetString());
        Assert.AreEqual("firewall.rule.action.unsupported", error.GetProperty("code").GetString());
        Assert.AreEqual("Action is invalid.", error.GetProperty("message").GetString());
    }

    [TestMethod]
    public void CreateValidation_ModelBindingExceptionUsesNonDiagnosticFallback()
    {
        ModelStateDictionary modelState = new();
        modelState.AddModelError("Count", string.Empty);

        ProblemDetails problem = ApiProblemDetailsFactory.CreateValidation(modelState);

        IReadOnlyList<ApiValidationError> errors = Assert.IsInstanceOfType<IReadOnlyList<ApiValidationError>>(
            problem.Extensions[ApiProblemDetails.VALIDATION_ERRORS_PROPERTY]);
        Assert.AreEqual(new ApiValidationError("Count", Code: null, "The value is invalid."), errors.Single());
    }
}
