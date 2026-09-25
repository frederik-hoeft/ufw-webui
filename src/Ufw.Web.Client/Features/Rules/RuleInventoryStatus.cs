namespace Ufw.Web.Client.Features.Rules;

internal enum RuleInventoryStatus
{
    NotLoaded,
    Loading,
    Current,
    Refreshing,
    Stale,
    Failed,
}
