using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.SystemState;
using Ufw.Shared.Parsing;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Iptables.Parsing;
using Ufw.Systemd.Interop.Iptables.Parsing.Terminals;

namespace Ufw.Systemd.Interop.Iptables.Grammars;

/// <summary>
/// Composition root for UFW <c>user.rules</c> / <c>user6.rules</c>.
/// Tuple translations inside <c>### RULES ###</c> are the only enrichment source.
/// </summary>
internal sealed class UfwUserRulesGrammar
{
    private const string RULES_SECTION = "RULES";

    private readonly IParser _grammar;

    private UfwUserRulesGrammar()
    {
        _grammar = Grammar.Sequence(
                Grammar.Repeat(Grammar.Alternative(
                    IptablesBlankLine.Instance,
                    IptablesTupleLine.Instance,
                    IptablesSectionLine.Instance,
                    IptablesCommentLine.Instance,
                    IptablesTableLine.Instance,
                    IptablesChainLine.Instance,
                    IptablesCommitLine.Instance,
                    IptablesRuleLine.Instance)),
                Grammar.Optional(HorizontalWhitespace.Instance))
            .RequireVisitor<IIptablesDocumentVisitor>();
    }

    public static UfwUserRulesGrammar Instance { get; } = new();

    public bool TryParse(string input, FirewallAddressFamily family, [NotNullWhen(true)] out IReadOnlyList<UfwUserRuleTranslation>? translations)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (family is not (FirewallAddressFamily.IPv4 or FirewallAddressFamily.IPv6))
        {
            throw new ArgumentOutOfRangeException(nameof(family));
        }

        if (!_grammar.TryParse(input, 0, out ISyntaxNode? syntaxNode, out int charsConsumed) || charsConsumed != input.Length)
        {
            translations = null;
            return false;
        }

        UserRulesDocumentBuilder builder = new(family);
        syntaxNode.Accept(builder);
        return builder.TryBuild(out translations);
    }

    private sealed class UserRulesDocumentBuilder(FirewallAddressFamily family) : IIptablesDocumentVisitor
    {
        private readonly List<UfwUserRuleTranslation> _translations = [];
        private readonly List<TranslatedIptablesRule> _currentRules = [];
        private bool _inRules;
        private bool _tupleOpen;
        private int _nextIndex;
        private FirewallRuleSpecification? _specification;
        private string? _ruleId;
        private bool _failed;
        private bool _sawFilter;
        private bool _tableOpen;

        public void VisitTable(string name)
        {
            if (_tableOpen)
            {
                _failed = true;
                return;
            }

            _tableOpen = true;
            if (name == "filter")
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
            if (!_tableOpen)
            {
                _failed = true;
            }
        }

        public void VisitRule(string chainName, IReadOnlyList<IptablesToken> arguments, string sourceLine)
        {
            if (_failed)
            {
                return;
            }

            if (!_tableOpen)
            {
                _failed = true;
                return;
            }

            if (!_inRules || !_tupleOpen)
            {
                return;
            }

            if (!IptablesRuleInterpreter.TryInterpret(arguments, family, out IReadOnlyList<FilterClause> clauses, out FilterTarget target))
            {
                _failed = true;
                return;
            }

            _currentRules.Add(new TranslatedIptablesRule
            {
                ChainName = chainName,
                Clauses = clauses,
                Target = target,
            });
        }

        public void VisitTuple(string body)
        {
            if (_failed || !_inRules)
            {
                _failed = true;
                return;
            }

            CloseTuple();
            if (!UfwTupleInterpreter.TryInterpret(body, family, out _specification, out _ruleId))
            {
                _failed = true;
                return;
            }

            _tupleOpen = true;
        }

        public void VisitSection(string name)
        {
            if (_failed)
            {
                return;
            }

            CloseTuple();
            _inRules = name == RULES_SECTION;
        }

        public void VisitCommit()
        {
            if (!_tableOpen)
            {
                _failed = true;
                return;
            }

            CloseTuple();
            _tableOpen = false;
            _inRules = false;
        }

        public bool TryBuild([NotNullWhen(true)] out IReadOnlyList<UfwUserRuleTranslation>? translations)
        {
            CloseTuple();
            if (_failed || !_sawFilter || _tableOpen)
            {
                translations = null;
                return false;
            }

            translations = _translations;
            return true;
        }

        private void CloseTuple()
        {
            if (!_tupleOpen)
            {
                return;
            }

            _translations.Add(new UfwUserRuleTranslation
            {
                Index = _nextIndex,
                Specification = _specification,
                RuleId = _ruleId,
                IptablesRules = _currentRules.ToArray(),
            });
            _nextIndex++;
            _currentRules.Clear();
            _specification = null;
            _ruleId = null;
            _tupleOpen = false;
        }
    }
}
