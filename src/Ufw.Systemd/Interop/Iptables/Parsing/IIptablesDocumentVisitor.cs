using Ufw.Shared.Parsing.Visitors;

namespace Ufw.Systemd.Interop.Iptables.Parsing;

internal interface IIptablesDocumentVisitor : INodeVisitor
{
    void VisitTable(string name);

    void VisitChain(string name, string policy);

    void VisitRule(string chainName, IReadOnlyList<IptablesToken> arguments, string sourceLine);

    void VisitTuple(string body);

    void VisitSection(string name);

    void VisitCommit();
}
