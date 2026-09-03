namespace Ufw.Ipc.Shared.Security.Intent;

/// <summary>
/// Well-known mutation operation identifiers bound into the signed intent.
/// </summary>
public static class IntentOperations
{
    public const string ADD_RULE = "rules.add";
    public const string DELETE_RULE = "rules.delete";
}
