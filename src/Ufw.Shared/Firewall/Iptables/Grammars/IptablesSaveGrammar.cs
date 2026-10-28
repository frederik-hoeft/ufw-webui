using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.SystemState;
using Ufw.Shared.Parsing;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Shared.Firewall.Iptables.Parsing;
using Ufw.Shared.Firewall.Iptables.Parsing.Lines;
using Ufw.Shared.Firewall.Iptables.Parsing.Terminals;

namespace Ufw.Shared.Firewall.Iptables.Grammars;

/// <summary>
/// Composition root for <c>iptables-save</c> / <c>ip6tables-save</c> output.
/// Only the filter table is retained. Other tables are accepted and ignored.
/// Produces a structured snapshot without installing firewall rules.
/// </summary>
public sealed class IptablesSaveGrammar
{
    private readonly IParser _grammar;

    private IptablesSaveGrammar()
    {
        _grammar = Grammar.Sequence(
                Grammar.Repeat(Grammar.Alternative(
                    IptablesBlankLine.Instance,
                    IptablesCommentLine.Instance,
                    IptablesTableLine.Instance,
                    IptablesChainLine.Instance,
                    IptablesCommitLine.Instance,
                    IptablesRuleLine.Instance)),
                Grammar.Optional(HorizontalWhitespace.Instance))
            .RequireVisitor<IIptablesDocumentVisitor>();
    }

    /// <summary>Reusable grammar instance.</summary>
    public static IptablesSaveGrammar Instance { get; } = new();

    /// <summary>Parses a complete save document and extracts its filter table.</summary>
    /// <param name="input">Complete iptables-save output.</param>
    /// <param name="family">Address family of the producing binary.</param>
    /// <param name="table">Parsed filter table, or <see langword="null"/> on failure.</param>
    /// <returns><see langword="false"/> for invalid documents or missing filter tables.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The address family is not IPv4 or IPv6.</exception>
    public bool TryParse(string input, FirewallAddressFamily family, [NotNullWhen(true)] out RawFilterTable? table)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (family is not (FirewallAddressFamily.IPv4 or FirewallAddressFamily.IPv6))
        {
            throw new ArgumentOutOfRangeException(nameof(family));
        }

        if (!_grammar.TryParse(input, 0, out ISyntaxNode? syntaxNode, out int charsConsumed) || charsConsumed != input.Length)
        {
            table = null;
            return false;
        }

        SaveDocumentBuilder builder = new(family);
        syntaxNode.Accept(builder);
        return builder.TryBuild(out table);
    }

    private sealed class SaveDocumentBuilder(FirewallAddressFamily family) : IIptablesDocumentVisitor
    {
        private readonly List<ChainBuilder> _chains = [];
        private readonly Dictionary<string, ChainBuilder> _chainsByName = new(StringComparer.Ordinal);
        private string? _table;
        private bool _capturing;
        private bool _sawFilter;
        private bool _failed;

        public void VisitTable(string name)
        {
            if (_table is not null)
            {
                _failed = true;
                return;
            }

            _table = name;
            _capturing = name == "filter";
            if (_capturing)
            {
                if (_sawFilter)
                {
                    _failed = true;
                    return;
                }

                _sawFilter = true;
            }
        }

        public void VisitChain(string name, string policy)
        {
            if (_failed)
            {
                return;
            }

            if (_table is null)
            {
                _failed = true;
                return;
            }

            if (!_capturing)
            {
                return;
            }

            if (!_chainsByName.TryAdd(name, new ChainBuilder(name, policy)))
            {
                _failed = true;
                return;
            }

            _chains.Add(_chainsByName[name]);
        }

        public void VisitRule(string chainName, IReadOnlyList<IptablesToken> arguments, string sourceLine, bool insert)
        {
            if (_failed)
            {
                return;
            }

            if (_table is null)
            {
                _failed = true;
                return;
            }

            if (!_capturing)
            {
                return;
            }

            if (!_chainsByName.TryGetValue(chainName, out ChainBuilder? chain)
                || !IptablesRuleInterpreter.TryInterpret(arguments, family, out IReadOnlyList<FilterClause> clauses, out FilterTarget target))
            {
                _failed = true;
                return;
            }

            chain.Rules.Add(new RawFilterRule
            {
                Location = new FilterRuleLocation(family, chainName, chain.Rules.Count),
                SourceLine = sourceLine,
                Clauses = clauses,
                Target = target,
            });
        }

        public void VisitTuple(string body) => _failed = true;

        public void VisitSection(string name) => _failed = true;

        public void VisitFlush(string chainName) => _failed = true;

        public void VisitCommit()
        {
            if (_table is null)
            {
                _failed = true;
                return;
            }

            _table = null;
            _capturing = false;
        }

        public bool TryBuild([NotNullWhen(true)] out RawFilterTable? table)
        {
            if (_failed || !_sawFilter || _table is not null)
            {
                table = null;
                return false;
            }

            HashSet<string> chainNames = new(StringComparer.Ordinal);
            foreach (ChainBuilder chain in _chains)
            {
                chainNames.Add(chain.Name);
            }

            List<RawFilterChain> chains = new(_chains.Count);
            foreach (ChainBuilder chain in _chains)
            {
                List<RawFilterRule> rules = new(chain.Rules.Count);
                foreach (RawFilterRule rule in chain.Rules)
                {
                    rules.Add(WithChainReference(rule, chainNames));
                }

                chains.Add(new RawFilterChain
                {
                    Name = chain.Name,
                    Policy = chain.Policy,
                    Rules = rules,
                });
            }

            table = new RawFilterTable
            {
                AddressFamily = family,
                Chains = chains,
            };
            return true;
        }

        private static RawFilterRule WithChainReference(RawFilterRule rule, HashSet<string> chainNames)
        {
            bool isReference = rule.Target.Kind != FilterTargetKind.None
                && rule.Target.Name is not null
                && chainNames.Contains(rule.Target.Name);
            if (isReference == rule.Target.IsChainReference)
            {
                return rule;
            }

            return new RawFilterRule
            {
                Location = rule.Location,
                SourceLine = rule.SourceLine,
                Clauses = rule.Clauses,
                Target = new FilterTarget
                {
                    Kind = rule.Target.Kind,
                    Name = rule.Target.Name,
                    IsChainReference = isReference,
                    Arguments = rule.Target.Arguments,
                    ParsedArguments = rule.Target.ParsedArguments,
                },
            };
        }
    }

    private sealed class ChainBuilder(string name, string policyText)
    {
        public string Name { get; } = name;

        public FilterChainPolicy Policy { get; } = policyText switch
        {
            "ACCEPT" => FilterChainPolicy.Accept,
            "DROP" => FilterChainPolicy.Drop,
            "-" => FilterChainPolicy.None,
            _ => throw new InvalidOperationException($"Unsupported chain policy '{policyText}'."),
        };

        public List<RawFilterRule> Rules { get; } = [];
    }
}
