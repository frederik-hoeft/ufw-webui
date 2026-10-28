using Ufw.Shared.Firewall.Iptables.Grammars;

namespace Ufw.Systemd.Tests.Interop.Iptables;

[TestClass]
public sealed class QuotedStringTokenTests
{
    [TestMethod]
    public void TryParse_DecodesBackslashesQuotesAndOtherEscapes()
    {
        const string line = """-A INPUT -m comment --comment 'Folder\\Path \'quoted\' \x' -j ACCEPT""";

        Assert.IsTrue(IptablesRestoreLineGrammar.Instance.TryParse(line, out IptablesRestoreStatement? statement));
        Assert.IsNotNull(statement.Arguments);
        Assert.AreEqual("Folder\\Path 'quoted' x", statement.Arguments[3].Text);
    }
}
