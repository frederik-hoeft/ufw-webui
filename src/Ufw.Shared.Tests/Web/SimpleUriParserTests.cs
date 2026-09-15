using Ufw.Shared.Web;

namespace Ufw.Shared.Tests.Web;

[TestClass]
public sealed class SimpleUriParserTests
{
    [TestMethod]
    public void Parse_AbsoluteUri_ExposesComponents()
    {
        SimpleUriParser parser = SimpleUriParser.Parse("https://example.test:8443/rules/list?scope=all&page=2");

        Assert.AreEqual("https", parser.Schema.ToString());
        Assert.AreEqual("example.test:8443", parser.Host.ToString());
        Assert.AreEqual("https://example.test:8443", parser.SchemaHost.ToString());
        Assert.AreEqual("/rules/list", parser.Path.ToString());
        Assert.AreEqual("https://example.test:8443/rules/list", parser.SchemaHostPath.ToString());
        Assert.AreEqual("scope=all&page=2", parser.Query.ToString());
    }

    [TestMethod]
    public void Parse_PathOnlyUri_DoesNotInventHost()
    {
        SimpleUriParser parser = SimpleUriParser.Parse("/rules/create?placement=before");

        Assert.AreEqual(string.Empty, parser.Schema.ToString());
        Assert.AreEqual(string.Empty, parser.Host.ToString());
        Assert.AreEqual(string.Empty, parser.SchemaHost.ToString());
        Assert.AreEqual("/rules/create", parser.Path.ToString());
    }

    [TestMethod]
    public void Enumerate_QueryParameters_PreservesRawValues()
    {
        SimpleUriParser parser = SimpleUriParser.Parse("/rules?first=one&empty=&token=a=b");
        SimpleUriParser.Enumerator enumerator = parser.GetEnumerator();

        Assert.IsTrue(enumerator.MoveNext());
        Assert.AreEqual("first", enumerator.Current.Key.ToString());
        Assert.AreEqual("one", enumerator.Current.Value.ToString());
        Assert.IsTrue(enumerator.MoveNext());
        Assert.AreEqual("empty", enumerator.Current.Key.ToString());
        Assert.AreEqual(string.Empty, enumerator.Current.Value.ToString());
        Assert.IsTrue(enumerator.MoveNext());
        Assert.AreEqual("token", enumerator.Current.Key.ToString());
        Assert.AreEqual("a=b", enumerator.Current.Value.ToString());
        Assert.IsFalse(enumerator.MoveNext());
    }

    [TestMethod]
    public void TryGetQueryParameter_FindsExactKey()
    {
        SimpleUriParser parser = SimpleUriParser.Parse("/rules?rule=one&ruleId=two");

        bool found = parser.TryGetQueryParameter("rule", out RouteDataRef routeData);

        Assert.IsTrue(found);
        Assert.AreEqual("one", routeData.Value.ToString());
        Assert.IsTrue(parser.ContainsQueryParameter("ruleId"));
        Assert.IsFalse(parser.ContainsQueryParameter("missing"));
    }

    [TestMethod]
    public void Parse_Whitespace_ReturnsEmptyParser()
    {
        SimpleUriParser parser = SimpleUriParser.Parse("   ");

        Assert.AreEqual(string.Empty, parser.Uri.ToString());
        Assert.AreEqual(string.Empty, parser.Path.ToString());
        Assert.AreEqual(string.Empty, parser.Query.ToString());
        Assert.IsFalse(parser.GetEnumerator().MoveNext());
    }
}
