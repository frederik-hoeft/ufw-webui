using Ufw.Client.Status;

namespace Ufw.Client.Components.Layout;

public sealed partial class OperationalStatusIndicator
{
    private readonly CancellationTokenSource _lifetime = new();

    protected async override Task OnInitializedAsync()
    {
        OperationalStatus.Changed += OnStatusChanged;
        if (OperationalStatus.Current.DaemonBackedApi == OperationalAvailability.Unknown)
        {
            try
            {
                await OperationalStatus.RefreshAsync(_lifetime.Token);
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
            }
        }
    }

    public void Dispose()
    {
        OperationalStatus.Changed -= OnStatusChanged;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    private void OnStatusChanged() => _ = InvokeAsync(StateHasChanged);

    private string Label => OperationalStatus.Current.DaemonBackedApi switch
    {
        OperationalAvailability.Available => NavigationText["DaemonConnected"],
        OperationalAvailability.Unavailable => NavigationText["DaemonUnavailable"],
        _ => OperationalStatus.IsRefreshing ? NavigationText["DaemonChecking"] : NavigationText["DaemonUnknown"],
    };

    private string StatusClass => OperationalStatus.Current.DaemonBackedApi switch
    {
        OperationalAvailability.Available => "app-navigation-status-dot-success",
        OperationalAvailability.Unavailable => "app-navigation-status-dot-error",
        _ => "app-navigation-status-dot-muted",
    };

    private string Tooltip
    {
        get
        {
            DateTimeOffset? checkedAt = OperationalStatus.Current.CheckedAt;
            return checkedAt is null
                ? NavigationText["NoStatusCheck"]
                : NavigationText["LastChecked", checkedAt.Value.ToLocalTime().ToString("g", System.Globalization.CultureInfo.CurrentCulture)];
        }
    }
}
