namespace Ufw.Systemd.Firewall.Replacement;

internal enum RuleReplacementTransactionKind
{
    UpdateExisting,
    InsertThenDelete,
}
