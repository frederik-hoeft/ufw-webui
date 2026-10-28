namespace Ufw.Mock.Formatting;

/// <summary>
/// A generated filter rule. The restore file describes the UFW command, while
/// iptables-save describes the installed rule (notably its normalized quoting).
/// </summary>
internal readonly record struct MockIptablesDeclaration(string Chain, string Arguments, string? ApplicationComment = null, bool Insert = false)
{
    public string ToRestoreLine() => Format(Insert ? "-I" : "-A", '\'');

    public string ToSaveLine()
    {
        if (ApplicationComment is null)
        {
            return "-A " + Chain + " " + Arguments;
        }

        // The UFW restore file appends the comment match after -j, while
        // iptables-save emits matches before the target.
        int target = Arguments.StartsWith("-j ", StringComparison.Ordinal) ? 0 : Arguments.IndexOf(" -j ", StringComparison.Ordinal);
        string comment = " -m comment --comment \"" + ApplicationComment + "\"";
        if (target == 0)
        {
            return "-A " + Chain + comment + " " + Arguments;
        }
        return "-A " + Chain + " " + (target > 0 ? Arguments.Insert(target, comment) : Arguments + comment);
    }

    private string Format(string operation, char quote) => $"{operation} {Chain} {Arguments}" + (ApplicationComment is null
        ? string.Empty
        : $" -m comment --comment {quote}{ApplicationComment}{quote}");
}
