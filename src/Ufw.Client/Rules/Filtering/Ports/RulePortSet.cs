using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Ufw.Client.Rules.Filtering;

namespace Ufw.Client.Rules.Filtering.Ports;

internal sealed class RulePortSet
{
    private readonly PortRange[] _ranges;

    private RulePortSet(PortRange[] ranges, string canonicalValue)
    {
        _ranges = ranges;
        CanonicalValue = canonicalValue;
    }

    public string CanonicalValue { get; }

    public static bool TryParse(string? value, [NotNullWhen(true)] out RulePortSet? ports)
    {
        ports = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        List<PortRange> ranges = [];
        string[] parts = value.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Any(static part => part.Length == 0))
        {
            return false;
        }

        foreach (string part in parts)
        {
            int separator = part.IndexOf(':', StringComparison.Ordinal);
            if (separator < 0)
            {
                if (!TryPort(part, out int port))
                {
                    return false;
                }
                ranges.Add(new PortRange(port, port));
                continue;
            }

            if (!TryPort(part[..separator], out int start) || !TryPort(part[(separator + 1)..], out int end) || start > end)
            {
                return false;
            }
            ranges.Add(new PortRange(start, end));
        }

        if (ranges.Count == 0)
        {
            return false;
        }

        ranges.Sort(static (left, right) => left.Start != right.Start ? left.Start.CompareTo(right.Start) : left.End.CompareTo(right.End));
        List<PortRange> merged = [];
        foreach (PortRange current in ranges)
        {
            if (merged.Count == 0 || current.Start > merged[^1].End + 1)
            {
                merged.Add(current);
            }
            else
            {
                PortRange previous = merged[^1];
                merged[^1] = new PortRange(previous.Start, Math.Max(previous.End, current.End));
            }
        }

        string canonical = string.Join(',', merged.Select(static range => range.Start == range.End
            ? range.Start.ToString(CultureInfo.InvariantCulture)
            : $"{range.Start.ToString(CultureInfo.InvariantCulture)}:{range.End.ToString(CultureInfo.InvariantCulture)}"));
        ports = new RulePortSet(merged.ToArray(), canonical);
        return true;
    }

    public bool Overlaps(RulePortSet other)
    {
        ArgumentNullException.ThrowIfNull(other);
        int leftIndex = 0;
        int rightIndex = 0;
        while (leftIndex < _ranges.Length && rightIndex < other._ranges.Length)
        {
            PortRange left = _ranges[leftIndex];
            PortRange right = other._ranges[rightIndex];
            if (left.End < right.Start)
            {
                leftIndex++;
            }
            else if (right.End < left.Start)
            {
                rightIndex++;
            }
            else
            {
                return true;
            }
        }
        return false;
    }

    private static bool TryPort(string value, out int port) => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out port) && port is >= 1 and <= 65535;

    private readonly record struct PortRange(int Start, int End);
}
