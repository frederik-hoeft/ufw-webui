using System.Text.RegularExpressions;
using Ufw.Systemd.Interop.Output.Grammars;
using Ufw.Systemd.Interop.Output.Model;

namespace Ufw.Systemd.Interop.Output;

internal sealed partial class UfwStatusParser
{
    public static UfwStatusSnapshot Parse(string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        bool active = false;
        List<ObservedUfwRule> rules = [];
        foreach (string rawLine in output.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            string line = rawLine.TrimEnd();
            if (line.StartsWith("Status:", StringComparison.OrdinalIgnoreCase))
            {
                active = line.Contains("active", StringComparison.OrdinalIgnoreCase)
                    && !line.Contains("inactive", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            Match numbered = NumberedRuleLine().Match(line);
            if (!numbered.Success)
            {
                continue;
            }

            int displayNumber = int.Parse(numbered.Groups["number"].Value, System.Globalization.CultureInfo.InvariantCulture);
            bool parsed = UfwListCommandResultGrammar.Instance.TryParse(line.Trim(), out UfwListCommandResultRow? row);
            rules.Add(new ObservedUfwRule
            {
                RawLine = line.Trim(),
                DisplayNumber = displayNumber,
                Parsed = parsed ? row : null,
            });
        }

        return new UfwStatusSnapshot(active, rules);
    }

    [GeneratedRegex(@"^\s*\[\s*(?<number>\d+)\]\s+", RegexOptions.CultureInvariant)]
    private static partial Regex NumberedRuleLine();
}

internal sealed record UfwStatusSnapshot(bool Active, IReadOnlyList<ObservedUfwRule> Rules);

internal sealed class ObservedUfwRule
{
    public required string RawLine { get; init; }

    public int DisplayNumber { get; init; }

    public UfwListCommandResultRow? Parsed { get; init; }
}
