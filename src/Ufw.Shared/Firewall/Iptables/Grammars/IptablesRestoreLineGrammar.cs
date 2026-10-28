using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Firewall.Iptables.Parsing;
using Ufw.Shared.Firewall.Iptables.Parsing.Lines;
using Ufw.Shared.Parsing;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;

namespace Ufw.Shared.Firewall.Iptables.Grammars;

/// <summary>
/// Parses one iptables-restore statement using the same lexical and line grammars
/// as the daemon's iptables-save and UFW translation parsers.
/// The caller interprets installation order and chain-state mutations.
/// This parser validates statement syntax, not kernel extension support or restore semantics.
/// </summary>
public sealed class IptablesRestoreLineGrammar
{
    private readonly IParser _grammar = Grammar.Alternative(
        IptablesTableLine.Instance,
        IptablesChainLine.Instance,
        IptablesRuleLine.AppendOrInsertInstance,
        IptablesFlushLine.Instance,
        IptablesCommitLine.Instance);

    /// <summary>Reusable grammar instance.</summary>
    public static IptablesRestoreLineGrammar Instance { get; } = new();

    /// <summary>Parses exactly one non-comment restore statement, consuming the complete input.</summary>
    /// <param name="line">Statement text without a trailing line ending.</param>
    /// <param name="statement">Typed statement on success; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the entire line matches the grammar.</returns>
    public bool TryParse(string line, [NotNullWhen(true)] out IptablesRestoreStatement? statement)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (!_grammar.TryParse(line, 0, out ISyntaxNode? node, out int consumed) || consumed != line.Length)
        {
            statement = null;
            return false;
        }

        StatementCollector collector = new();
        node.Accept(collector);
        statement = collector.Statement;
        return statement is not null;
    }

    private sealed class StatementCollector : IIptablesDocumentVisitor
    {
        public IptablesRestoreStatement? Statement { get; private set; }

        public void VisitTable(string name) => Statement = new(IptablesRestoreOperation.Table, name);

        public void VisitChain(string name, string policy) => Statement = new(IptablesRestoreOperation.Chain, name, policy);

        public void VisitRule(string chainName, IReadOnlyList<IptablesToken> arguments, string sourceLine, bool insert) =>
            Statement = new(insert ? IptablesRestoreOperation.Insert : IptablesRestoreOperation.Append,
                chainName, Arguments: arguments, SourceLine: sourceLine);

        public void VisitFlush(string chainName) => Statement = new(IptablesRestoreOperation.Flush, chainName);

        public void VisitCommit() => Statement = new(IptablesRestoreOperation.Commit);

        public void VisitTuple(string body) { }

        public void VisitSection(string name) { }
    }
}
