using Ufw.Shared.Parsing.Visitors;

namespace Ufw.Shared.Firewall.Iptables.Parsing;

internal interface IIptablesDocumentVisitor : INodeVisitor
{
    void VisitTable(string name);

    void VisitChain(string name, string policy);

    void VisitRule(string chainName, IReadOnlyList<IptablesToken> arguments, string sourceLine, bool insert);

    void VisitTuple(string body);

    void VisitSection(string name);

    void VisitFlush(string chainName);

    void VisitCommit();
}
