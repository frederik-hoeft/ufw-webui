using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Ufw.Web.Api.V1.Models.Auth;

namespace Ufw.Web.Tests.Api.V1.Models.Auth;

[TestClass]
public sealed class LoginRequestTests
{
    [TestMethod]
    public void ValidationAttributes_AreAppliedToPrimaryConstructorParameters()
    {
        ParameterInfo[] parameters = typeof(LoginRequest).GetConstructors().Single().GetParameters();
        ParameterInfo email = parameters.Single(static parameter => parameter.Name == nameof(LoginRequest.Email));
        ParameterInfo password = parameters.Single(static parameter => parameter.Name == nameof(LoginRequest.Password));

        Assert.IsNotNull(email.GetCustomAttribute<RequiredAttribute>());
        Assert.IsNotNull(email.GetCustomAttribute<EmailAddressAttribute>());
        Assert.IsNotNull(password.GetCustomAttribute<RequiredAttribute>());
    }
}
