using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Shared.Firewall.Iptables.Parsing;
using Ufw.Shared.Firewall.Iptables.Parsing.Terminals;

namespace Ufw.Systemd.Tests.Interop.Iptables;

[TestClass]
public sealed class QuotedStringTokenTests
{
    [TestMethod]
    public void TryParse_DecodesBackslashesQuotesAndOtherEscapes()
    {
        const string input = """'Folder\\Path \'quoted\' \x'""";

        Assert.IsTrue(QuotedStringToken.Instance.TryParse(input, 0, out ISyntaxNode? node, out int consumed));
        Assert.AreEqual(input.Length, consumed);
        IptablesTokenCollector collector = new();
        node.Accept(collector);
        Assert.HasCount(1, collector.Tokens);
        Assert.AreEqual("Folder\\Path 'quoted' x", collector.Tokens[0].Text);
    }
}
