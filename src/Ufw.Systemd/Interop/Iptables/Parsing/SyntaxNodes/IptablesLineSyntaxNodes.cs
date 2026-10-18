using Ufw.Shared.Parsing.SyntaxNodes;

namespace Ufw.Systemd.Interop.Iptables.Parsing.SyntaxNodes;

internal sealed class IgnoredIptablesLineSyntaxNode(string? name) : SyntaxNodeBase(name);

internal sealed class TableLineSyntaxNode(string? name, string tableName) : SyntaxNodeBase<IIptablesDocumentVisitor>(name)
{
    protected override void Accept(IIptablesDocumentVisitor visitor) => visitor.VisitTable(tableName);
}

internal sealed class ChainLineSyntaxNode(string? name, string chainName, string policy) : SyntaxNodeBase<IIptablesDocumentVisitor>(name)
{
    protected override void Accept(IIptablesDocumentVisitor visitor) => visitor.VisitChain(chainName, policy);
}

internal sealed class RuleLineSyntaxNode(string? name, string chainName, IReadOnlyList<IptablesToken> arguments, string sourceLine) : SyntaxNodeBase<IIptablesDocumentVisitor>(name)
{
    protected override void Accept(IIptablesDocumentVisitor visitor) => visitor.VisitRule(chainName, arguments, sourceLine);
}

internal sealed class TupleLineSyntaxNode(string? name, string body) : SyntaxNodeBase<IIptablesDocumentVisitor>(name)
{
    protected override void Accept(IIptablesDocumentVisitor visitor) => visitor.VisitTuple(body);
}

internal sealed class SectionLineSyntaxNode(string? name, string sectionName) : SyntaxNodeBase<IIptablesDocumentVisitor>(name)
{
    protected override void Accept(IIptablesDocumentVisitor visitor) => visitor.VisitSection(sectionName);
}

internal sealed class CommitLineSyntaxNode(string? name) : SyntaxNodeBase<IIptablesDocumentVisitor>(name)
{
    protected override void Accept(IIptablesDocumentVisitor visitor) => visitor.VisitCommit();
}
