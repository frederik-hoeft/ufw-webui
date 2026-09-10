using MudBlazor;
using Ufw.Client.Status;
using Ufw.Shared.Security.Intent;

namespace Ufw.Client.Pages;

public sealed partial class Status
{
    private readonly CancellationTokenSource _lifetime = new();

    private IReadOnlyList<BreadcrumbItem> Breadcrumbs =>
    [
        new BreadcrumbItem(StatusText["SystemBreadcrumb"], null, disabled: true),
        new BreadcrumbItem(StatusText["StatusBreadcrumb"], null, disabled: true),
    ];

    protected async override Task OnInitializedAsync()
    {
        OperationalStatus.Changed += OnStatusChanged;
        if (OperationalStatus.Current.CheckedAt is null && !OperationalStatus.IsRefreshing)
        {
            await RefreshAsync();
        }
    }

    public void Dispose()
    {
        OperationalStatus.Changed -= OnStatusChanged;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    private async Task RefreshAsync()
    {
        try
        {
            await OperationalStatus.RefreshAsync(_lifetime.Token);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
    }

    private void OnStatusChanged() => _ = InvokeAsync(StateHasChanged);

    private string DaemonStatusLabel => OperationalStatus.Current.DaemonBackedApi switch
    {
        OperationalAvailability.Available when OperationalStatus.Current.IntentProtocolCompatible == false
            => StatusText["ConnectedIncompatible"],
        OperationalAvailability.Available => StatusText["Connected"],
        OperationalAvailability.Unavailable => CommonText["Unavailable"],
        _ => OperationalStatus.IsRefreshing ? CommonText["Checking"] : CommonText["Unknown"],
    };

    private string DaemonChipClass => OperationalStatus.Current.DaemonBackedApi switch
    {
        OperationalAvailability.Available when OperationalStatus.Current.IntentProtocolCompatible == false
            => "page-status-chip page-status-chip-warning",
        OperationalAvailability.Available => "page-status-chip page-status-chip-success",
        OperationalAvailability.Unavailable => "page-status-chip page-status-chip-error",
        _ => "page-status-chip",
    };

    private Color DaemonIconColor => OperationalStatus.Current.DaemonBackedApi switch
    {
        OperationalAvailability.Available when OperationalStatus.Current.IntentProtocolCompatible == false => Color.Warning,
        OperationalAvailability.Available => Color.Success,
        OperationalAvailability.Unavailable => Color.Error,
        _ => Color.Secondary,
    };

    private string FirewallStatusLabel => OperationalStatus.Current.FirewallSnapshot switch
    {
        OperationalAvailability.Available when OperationalStatus.Current.FirewallActive == true => CommonText["Active"],
        OperationalAvailability.Available when OperationalStatus.Current.FirewallActive == false => CommonText["Inactive"],
        OperationalAvailability.Unavailable => CommonText["Unavailable"],
        _ => OperationalStatus.IsRefreshing ? CommonText["Checking"] : CommonText["Unknown"],
    };

    private Color FirewallIconColor => OperationalStatus.Current.FirewallSnapshot switch
    {
        OperationalAvailability.Available when OperationalStatus.Current.FirewallActive == true => Color.Success,
        OperationalAvailability.Available when OperationalStatus.Current.FirewallActive == false => Color.Warning,
        OperationalAvailability.Unavailable => Color.Error,
        _ => Color.Secondary,
    };

    private string DescribeIntentProtocol()
    {
        if (OperationalStatus.Current.IntentProtocolVersion is not { } version)
        {
            return CommonText["Unavailable"];
        }

        return OperationalStatus.Current.IntentProtocolCompatible == true
            ? StatusText["Compatible", version]
            : StatusText["ClientSupports", version, IntentProtocol.VERSION];
    }

    private string DescribeRuleCount() => OperationalStatus.Current.RuleCount switch
    {
        null => CommonText["Unavailable"],
        1 => StatusText["RuleCountOne"],
        int count => StatusText["RuleCountMany", count.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)],
    };

    private string ValueOrUnavailable(string? value) => string.IsNullOrWhiteSpace(value) ? CommonText["Unavailable"] : value;

    private string DescribeDuration(TimeSpan duration)
    {
        System.Globalization.CultureInfo culture = System.Globalization.CultureInfo.CurrentCulture;
        return duration.TotalMilliseconds < 1000
            ? StatusText["DurationMilliseconds", duration.TotalMilliseconds.ToString("0", culture)]
            : StatusText["DurationSeconds", duration.TotalSeconds.ToString("0.0", culture)];
    }

    private static string FormatLocalDateTime(DateTimeOffset value) =>
        value.ToLocalTime().ToString("g", System.Globalization.CultureInfo.CurrentCulture);
}
