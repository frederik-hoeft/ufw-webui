namespace Ufw.Web.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
internal sealed class RequireAntiforgeryValidationAttribute : Attribute;
