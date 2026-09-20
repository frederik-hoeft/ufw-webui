namespace Ufw.Client.Rules;

internal enum RuleInventoryStatus
{
    NotLoaded,
    Loading,
    Current,
    Refreshing,
    Stale,
    Failed,
}
