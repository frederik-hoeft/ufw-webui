using Microsoft.AspNetCore.Components;
using MudBlazor;
using Ufw.Client.Components.Rules;
using Ufw.Client.Errors;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Client.Pages;

public sealed partial class CreateRule
{
    private readonly CancellationTokenSource _lifetime = new();
    private RulesPageState _state = RulesPageState.Initial;
    private FirewallRuleSpecification _draft = FirewallRuleDefaults.Create();
    private string _privateKey = string.Empty;
    private string? _reconciliationRuleIdentity;
    private bool _mutationMayHaveCompleted;
    private bool _submitting;

    private IReadOnlyList<BreadcrumbItem> Breadcrumbs =>
    [
        new BreadcrumbItem(RulesText["FirewallBreadcrumb"], null, disabled: true),
        new BreadcrumbItem(RulesText["RulesBreadcrumb"], "/rules"),
        new BreadcrumbItem(RulesText["AddRuleBreadcrumb"], null, disabled: true),
    ];

    private bool IsOrderedInsertionRequested
        => !string.IsNullOrWhiteSpace(BeforeRuleId) || !string.IsNullOrWhiteSpace(AfterRuleId);

    private bool CanEdit => _state.IsCurrent && !_submitting && !_mutationMayHaveCompleted;

    private bool CanSubmit => CanEdit && !IsOrderedInsertionRequested;

    private string HeaderDescription => IsOrderedInsertionRequested
        ? RulesText["CreateOrderedDescription"]
        : RulesText["CreateDescription"];

    private string RuleDefinitionDescription => IsOrderedInsertionRequested
        ? RulesText["OrderedRuleDefinitionDescription"]
        : RulesText["DefinitionDescription"];

    [Parameter, SupplyParameterFromQuery(Name = "before")]
    public string? BeforeRuleId { get; set; }

    [Parameter, SupplyParameterFromQuery(Name = "after")]
    public string? AfterRuleId { get; set; }

    protected async override Task OnInitializedAsync() => await LoadRulesAsync(RuleRefreshReason.Manual);

    public void Dispose()
    {
        _privateKey = string.Empty;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    private string DescribeRuleCount(int count) => count == 1
        ? RulesText["CurrentRuleCountOne"]
        : RulesText["CurrentRuleCountMany", count.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)];

    private string DescribeSnapshotStatus()
    {
        if (_state.IsStale)
        {
            return RulesText["AuthoritativeSnapshotStale"];
        }

        return _state.Status == RulesPageStatus.Refreshing
            ? RulesText["AuthoritativeSnapshotRefreshing"]
            : RulesText["AuthoritativeSnapshotCurrent"];
    }

    private Task RefreshAsync()
    {
        if (_submitting || _state.IsLoading)
        {
            return Task.CompletedTask;
        }

        return LoadRulesAsync(RuleRefreshReason.Manual);
    }

    private async Task LoadRulesAsync(RuleRefreshReason reason)
    {
        if (_state.IsLoading)
        {
            return;
        }

        _state = _state.BeginRefresh(reason);
        try
        {
            RuleListResponse response = await RuleApiClient.GetRulesAsync(_lifetime.Token);
            _state = RulesPageState.CompleteRefresh(response);

            if (_mutationMayHaveCompleted)
            {
                if (_reconciliationRuleIdentity is not null && ContainsRuleIdentity(_state.Snapshot!, _reconciliationRuleIdentity))
                {
                    Navigation.NavigateTo("/rules");
                    return;
                }

                _mutationMayHaveCompleted = false;
                _reconciliationRuleIdentity = null;
                Snackbar.Add(RulesText["SubmittedRuleMissing"], Severity.Warning);
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (ClientErrors.TryDescribe(exception, out _))
        {
            _state = _state.FailRefresh(ClientErrors.Describe(exception));
        }
    }

    private async Task AddRuleAsync()
    {
        if (!CanSubmit || string.IsNullOrWhiteSpace(_privateKey))
        {
            return;
        }

        FirewallRuleSpecification normalized = RuleSpecificationNormalizer.Normalize(_draft);
        // Keep the requested identity as the fallback for an uncertain outcome where no
        // mutation response is available. A successful family-neutral add returns a
        // concrete family-specific rule, so prefer that identity for the subsequent
        // authoritative refresh.
        _reconciliationRuleIdentity = RuleIdentity.Compute(normalized);
        _submitting = true;
        try
        {
            RuleMutationResponse mutation = await RuleMutations.AddRuleAsync(normalized, _privateKey, _lifetime.Token);
            _reconciliationRuleIdentity = GetRuleIdentity(mutation.Rule) ?? _reconciliationRuleIdentity;
            _mutationMayHaveCompleted = true;
            _submitting = false;
            await LoadRulesAsync(RuleRefreshReason.AfterMutation);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (ClientErrors.TryDescribe(exception, out _))
        {
            ClientError error = ClientErrors.Describe(exception);
            _state = _state.AfterMutationFailure(error);
            _mutationMayHaveCompleted = _state.StaleReason == RuleSnapshotStaleReason.MutationOutcomeUnknown;
            if (!_mutationMayHaveCompleted)
            {
                _reconciliationRuleIdentity = null;
            }
            Snackbar.Add(error.Message, Severity.Error);
        }
        finally
        {
            _privateKey = string.Empty;
            _submitting = false;
        }
    }

    private void Cancel()
    {
        if (!_submitting)
        {
            _privateKey = string.Empty;
            Navigation.NavigateTo("/rules");
        }
    }

    private string DescribeInsertionTarget()
    {
        if (!string.IsNullOrWhiteSpace(BeforeRuleId) && !string.IsNullOrWhiteSpace(AfterRuleId))
        {
            return RulesText["BothInsertionTargets"];
        }

        string? ruleId = BeforeRuleId ?? AfterRuleId;
        if (string.IsNullOrWhiteSpace(ruleId) || _state.Snapshot is null)
        {
            return RulesText["InsertionTargetUnavailable"];
        }

        ListedFirewallRule[] matches = _state.Snapshot.Rules
            .Where(rule => string.Equals(rule.RuleId, ruleId, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            return matches.Length == 0
                ? RulesText["InsertionTargetMissing"]
                : RulesText["InsertionTargetAmbiguous"];
        }

        ListedFirewallRule target = matches[0];
        string position = target.DisplayNumber is { } displayNumber
            ? RulesText["RulePosition", displayNumber.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)]
            : RulesText["SelectedRule"];
        string placement = !string.IsNullOrWhiteSpace(BeforeRuleId) ? RulesText["Before"] : RulesText["After"];
        return RulesText["InsertTargetDescription", placement, position];
    }

    private string DescribeStaleState() => _state.StaleReason switch
    {
        RuleSnapshotStaleReason.MutationCommitted => RulesText["RefreshAfterMutationFailed"],
        RuleSnapshotStaleReason.MutationOutcomeUnknown => RulesText["MutationOutcomeUnknown"],
        RuleSnapshotStaleReason.MutationRejectedRequiresRefresh => RulesText["MutationRejectedRefresh"],
        _ => RulesText["LatestRefreshFailed"],
    };

    private static string? GetRuleIdentity(ListedFirewallRule rule)
    {
        if (!string.IsNullOrWhiteSpace(rule.RuleId))
        {
            return rule.RuleId;
        }

        return rule.Parsed && rule.Rule is not null ? RuleIdentity.Compute(rule.Rule) : null;
    }

    private static bool ContainsRuleIdentity(RuleSnapshot snapshot, string identity)
    {
        return snapshot.Rules.Any(rule => rule.Parsed
            && rule.Rule is not null
            && string.Equals(rule.RuleId ?? RuleIdentity.Compute(rule.Rule), identity, StringComparison.Ordinal));
    }
}
