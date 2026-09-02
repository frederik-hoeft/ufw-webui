namespace Ufw.Roslyn.Controllers.Mapping.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class ApiControllerRegistrationAttribute<TController> : Attribute where TController : ControllerBase;
