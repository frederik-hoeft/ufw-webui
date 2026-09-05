using Microsoft.Extensions.Configuration;
using Ufw.Web.Configuration;

namespace Ufw.Web.Tests.Configuration;

[TestClass]
public sealed class AuthenticationBootstrapOptionsTests
{
    [TestMethod]
    public void Bind_IndexedUserConfiguration_BindsUsers()
    {
        Dictionary<string, string?> values = new()
        {
            ["Auth:Bootstrap:Users:0:Email"] = "first@example.invalid",
            ["Auth:Bootstrap:Users:0:Password"] = "FirstPassword1234",
            ["Auth:Bootstrap:Users:1:Email"] = "second@example.invalid",
            ["Auth:Bootstrap:Users:1:UserName"] = "second-user",
            ["Auth:Bootstrap:Users:1:EmailConfirmed"] = "false",
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        AuthenticationBootstrapOptions? options = configuration
            .GetSection(AuthenticationBootstrapOptions.SECTION_NAME)
            .Get<AuthenticationBootstrapOptions>();

        Assert.IsNotNull(options);
        Assert.HasCount(2, options.Users);
        Assert.AreEqual("first@example.invalid", options.Users[0].Email);
        Assert.AreEqual("FirstPassword1234", options.Users[0].Password);
        Assert.IsTrue(options.Users[0].EmailConfirmed);
        Assert.AreEqual("second-user", options.Users[1].UserName);
        Assert.IsFalse(options.Users[1].EmailConfirmed);
    }
}
