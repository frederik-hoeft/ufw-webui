using Ufw.Client.Components.Rules;

namespace Ufw.Client.Tests.Rules;

[TestClass]
public sealed class RuleMatchTextSnippetTests
{
    [TestMethod]
    public void Create_ClipsLongTextAroundMatch()
    {
        string value = "012345678901234567890123456789MATCHabcdefghijklmnopqrstuvwxyz";
        int start = value.IndexOf("MATCH", StringComparison.Ordinal);

        RuleMatchTextSnippet snippet = RuleMatchTextSnippet.Create(value, start, "MATCH".Length, contextLength: 20);

        Assert.AreEqual("01234567890123456789", snippet.Prefix);
        Assert.AreEqual("MATCH", snippet.Match);
        Assert.AreEqual("abcdefghijklmnopqrst", snippet.Suffix);
        Assert.IsTrue(snippet.HasLeadingEllipsis);
        Assert.IsTrue(snippet.HasTrailingEllipsis);
    }

    [TestMethod]
    public void Create_DoesNotInventEllipsesAtTextBoundaries()
    {
        RuleMatchTextSnippet snippet = RuleMatchTextSnippet.Create("MATCH suffix", 0, "MATCH".Length, contextLength: 20);

        Assert.AreEqual(string.Empty, snippet.Prefix);
        Assert.AreEqual("MATCH", snippet.Match);
        Assert.AreEqual(" suffix", snippet.Suffix);
        Assert.IsFalse(snippet.HasLeadingEllipsis);
        Assert.IsFalse(snippet.HasTrailingEllipsis);
    }
}
