using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Output.Grammars;
using Ufw.Systemd.Interop.Output.Model;
using Ufw.Systemd.Interop.Output.Parsers;
using Ufw.Systemd.Interop.Output.SyntaxNodes;

namespace Ufw.Systemd.Interop.Output;

internal sealed class UfwStatusParser
{
    public static UfwStatusSnapshot? Parse(string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        bool? active = null;
        List<ObservedUfwRule> rules = [];
        foreach (string rawLine in output.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            string line = rawLine.TrimEnd();
            if (line.StartsWith("Status:", StringComparison.OrdinalIgnoreCase))
            {
                string status = line["Status:".Length..].Trim();
                if (status.Equals("active", StringComparison.OrdinalIgnoreCase))
                {
                    active = true;
                }
                else if (status.Equals("inactive", StringComparison.OrdinalIgnoreCase))
                {
                    active = false;
                }
                else
                {
                    return null;
                }
                continue;
            }

            string trimmed = line.Trim();
            if (UfwListCommandResultGrammar.Instance.TryParse(trimmed, out UfwListCommandResultRow? parsed))
            {
                rules.Add(new ObservedUfwRule
                {
                    RawLine = trimmed,
                    DisplayNumber = parsed.RowNumber,
                    Parsed = parsed,
                });
                continue;
            }

            if (!RowNumber.Instance.TryParse(trimmed, 0, out ISyntaxNode? rowNumberNode, out _) || rowNumberNode is not RowNumberSyntaxNode rowNumber)
            {
                continue;
            }

            rules.Add(new ObservedUfwRule
            {
                RawLine = trimmed,
                DisplayNumber = rowNumber.Evaluate(),
                Parsed = null,
            });
        }

        return active.HasValue ? new UfwStatusSnapshot(active.Value, rules) : null;
    }
}
