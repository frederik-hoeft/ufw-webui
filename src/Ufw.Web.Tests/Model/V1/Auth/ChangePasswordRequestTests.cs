using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Ufw.Web.Model.V1.Auth;

namespace Ufw.Web.Tests.Model.V1.Auth;

[TestClass]
public sealed class ChangePasswordRequestTests
{
    [TestMethod]
    public void ValidationAttributes_RequireBothPasswords()
    {
        ParameterInfo[] parameters = typeof(ChangePasswordRequest).GetConstructors().Single().GetParameters();
        ParameterInfo currentPassword = parameters.Single(static parameter => parameter.Name == nameof(ChangePasswordRequest.CurrentPassword));
        ParameterInfo newPassword = parameters.Single(static parameter => parameter.Name == nameof(ChangePasswordRequest.NewPassword));

        Assert.IsNotNull(currentPassword.GetCustomAttribute<RequiredAttribute>());
        Assert.IsNotNull(newPassword.GetCustomAttribute<RequiredAttribute>());
    }
}
