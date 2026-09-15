using System.Globalization;
using Ufw.Shared.Web;

namespace Ufw.Shared.Tests.Web;

[TestClass]
public sealed class SimpleUriBuilderTests
{
    [TestMethod]
    [DataRow("api/v1", "rules", "api/v1/rules")]
    [DataRow("api/v1/", "rules", "api/v1/rules")]
    [DataRow("api/v1", "/rules", "api/v1/rules")]
    [DataRow("api/v1/", "/rules", "api/v1/rules")]
    public void AppendPath_NormalizesJoinDelimiter(string baseUri, string path, string expected)
    {
        string result = SimpleUriBuilder.Create(baseUri).AppendPath(path).Build();

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void AppendQuery_EncodesKeysAndValues()
    {
        string result = SimpleUriBuilder.Create("/rules/create")
            .AppendQuery("return url", "/rules/create?filter=a b&name=\u00e4")
            .Build();

        Assert.AreEqual("/rules/create?return%20url=%2Frules%2Fcreate%3Ffilter%3Da%20b%26name%3D%C3%A4", result);
    }

    [TestMethod]
    public void AppendQuery_FormattableValue_UsesInvariantCulture()
    {
        string result = SimpleUriBuilder.Create("/rules")
            .AppendQuery("value", new CultureAwareValue())
            .Build();

        Assert.AreEqual("/rules?value=invariant", result);
    }

    [TestMethod]
    public void AppendQuery_ExistingEmptyQuery_DoesNotIntroduceEmptyParameter()
    {
        string result = SimpleUriBuilder.Create("/rules?")
            .AppendQuery("page", 2)
            .AppendQuery("enabled", true)
            .Build();

        Assert.AreEqual("/rules?page=2&enabled=true", result);
    }

    [TestMethod]
    public void AppendQuery_ExistingQuery_AppendsWithAmpersand()
    {
        string result = SimpleUriBuilder.Create("/rules?scope=all")
            .AppendQuery("page", 2)
            .Build();

        Assert.AreEqual("/rules?scope=all&page=2", result);
    }

    [TestMethod]
    public void AppendPath_AfterQuery_Throws()
    {
        SimpleUriBuilder builder = SimpleUriBuilder.Create("/rules").AppendQuery("page", 2);

        Assert.ThrowsExactly<InvalidOperationException>(() => builder.AppendPath("details"));
    }

    [TestMethod]
    public void AppendQuery_WhitespaceKey_Throws()
    {
        SimpleUriBuilder builder = SimpleUriBuilder.Create("/rules");

        Assert.ThrowsExactly<ArgumentException>(() => builder.AppendQuery(" ", "value"));
    }

    [TestMethod]
    public void BuildUri_PreservesRequestedUriKind()
    {
        Uri uri = SimpleUriBuilder.Create("api/v1/rules").AppendPath("1").BuildUri(UriKind.Relative);

        Assert.IsFalse(uri.IsAbsoluteUri);
        Assert.AreEqual("api/v1/rules/1", uri.OriginalString);
    }

    private sealed class CultureAwareValue : IFormattable
    {
        public string ToString(string? format, IFormatProvider? formatProvider)
        {
            _ = format;
            return Equals(formatProvider, CultureInfo.InvariantCulture) ? "invariant" : "culture-specific";
        }

        public override string ToString() => "default";
    }
}
