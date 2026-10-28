using Ufw.Shared.Firewall.Iptables.Parsing;

namespace Ufw.Shared.Firewall.Iptables.Grammars;

/// <summary>Supported statement kinds in an iptables-restore document.</summary>
public enum IptablesRestoreOperation { Table, Chain, Append, Insert, Flush, Commit }

/// <summary>
/// One syntactically valid iptables-restore statement. The argument tokens retain their
/// decoded text and lexical categories; executing the operation is the caller's responsibility.
/// </summary>
/// <param name="Operation">The parsed operation.</param>
/// <param name="Name">Table or chain name, when applicable.</param>
/// <param name="Policy">Chain policy on declarations, when applicable.</param>
/// <param name="Arguments">Rule arguments, excluding the command and chain name.</param>
/// <param name="SourceLine">Original rule line, when applicable.</param>
public sealed record IptablesRestoreStatement(
    IptablesRestoreOperation Operation,
    string? Name = null,
    string? Policy = null,
    IReadOnlyList<IptablesToken>? Arguments = null,
    string? SourceLine = null);
