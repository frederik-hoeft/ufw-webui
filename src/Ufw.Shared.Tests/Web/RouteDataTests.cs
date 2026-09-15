using Ufw.Shared.Web;

namespace Ufw.Shared.Tests.Web;

[TestClass]
public sealed class RouteDataTests
{
    [TestMethod]
    public void Create_NullValue_NormalizesToEmptyString()
    {
        RouteData routeData = RouteData.Create("key", value: null);

        Assert.AreEqual("key", routeData.Key);
        Assert.AreEqual(string.Empty, routeData.Value);
    }

    [TestMethod]
    public void Conversions_PreserveKeyAndValue()
    {
        RouteData original = RouteData.Create("page", 7);

        RouteDataRef reference = original;
        RouteData copy = (RouteData)reference;

        Assert.AreEqual("page", reference.Key.ToString());
        Assert.AreEqual("7", reference.Value.ToString());
        Assert.AreEqual(original.Key, copy.Key);
        Assert.AreEqual(original.Value, copy.Value);
    }
}
