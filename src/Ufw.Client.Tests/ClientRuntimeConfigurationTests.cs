using Microsoft.Extensions.Configuration;
using Ufw.Client.Configuration;

namespace Ufw.Client.Tests;

[TestClass]
public sealed class ClientRuntimeConfigurationTests
{
    [TestMethod]
    [DataRow("https://api.example.invalid", "https://api.example.invalid/")]
    [DataRow("api", "https://app.example.invalid/base/api/")]
    [DataRow("/api", "https://app.example.invalid/api/")]
    public void GetApiBaseAddress_AcceptsHttpsAbsoluteOrApplicationRelativeValues(string configured, string expected)
    {
        IConfiguration configuration = Configuration(configured);

        Uri result = ClientRuntimeConfiguration.GetApiBaseAddress(configuration, new Uri("https://app.example.invalid/base/"));

        Assert.AreEqual(expected, result.AbsoluteUri);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("http://api.example.invalid")]
    [DataRow("//other.example.invalid/api")]
    [DataRow("https://user:pass@api.example.invalid")]
    [DataRow("https://api.example.invalid?query=1")]
    [DataRow("https://api.example.invalid/#fragment")]
    public void GetApiBaseAddress_RejectsUnsafeOrAmbiguousValues(string? configured)
    {
        IConfiguration configuration = Configuration(configured);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ClientRuntimeConfiguration.GetApiBaseAddress(configuration, new Uri("https://app.example.invalid/base/")));
    }

    private static IConfiguration Configuration(string? apiBaseUrl) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ApiBaseUrl"] = apiBaseUrl }).Build();
}
