namespace Ufw.Roslyn.Controllers.Mapping.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public sealed class ApiControllerRegistrationAttribute<TController> : Attribute where TController : ControllerBase;
