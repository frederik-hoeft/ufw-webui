using System.Globalization;
using Ufw.Shared.Domain.Algebra;

namespace Ufw.Shared.Domain;

/// <summary>
/// Port domain shared by every modeled protocol. The closed world is <c>1..65535</c>.
/// Null, blank, and <c>any</c> parse as that whole domain.
/// </summary>
public static class PacketPorts
{
    /// <summary>Gets the smallest modeled port.</summary>
    public const ushort MINIMUM = 1;

    /// <summary>Gets the largest modeled port.</summary>
    public const ushort MAXIMUM = 65535;

    /// <summary>Gets every modeled port.</summary>
    public static IntervalSet<ushort> Universe { get; } = IntervalSet<ushort>.Between(MINIMUM, MAXIMUM);

    /// <summary>
    /// Parses a comma-separated list of ports and inclusive <c>start:end</c> ranges.
    /// </summary>
    public static IntervalSet<ushort> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Trim().Equals("any", StringComparison.OrdinalIgnoreCase))
        {
            return Universe;
        }

        List<Interval<ushort>> ranges = [];
        string trimmed = text.Trim();
        int start = 0;
        for (int index = 0; index <= trimmed.Length; index++)
        {
            if (index != trimmed.Length && trimmed[index] != ',')
            {
                continue;
            }

            if (index == start)
            {
                throw new FormatException($"'{text}' contains an empty port entry.");
            }

            ranges.Add(ParsePart(trimmed[start..index]));
            start = index + 1;
        }

        return IntervalSet<ushort>.Of(ranges);
    }

    private static Interval<ushort> ParsePart(string part)
    {
        int colon = part.IndexOf(':');
        if (colon < 0)
        {
            ushort port = ParsePort(part);
            return new Interval<ushort>(port, port);
        }

        if (colon != part.LastIndexOf(':') || colon == 0 || colon == part.Length - 1)
        {
            throw new FormatException($"'{part}' is not a port range.");
        }

        ushort rangeStart = ParsePort(part[..colon]);
        ushort rangeEnd = ParsePort(part[(colon + 1)..]);
        if (rangeStart > rangeEnd)
        {
            throw new FormatException($"'{part}' has a descending port range.");
        }

        return new Interval<ushort>(rangeStart, rangeEnd);
    }

    private static ushort ParsePort(string token)
    {
        if (token.Length == 0 || token.Length > 5 || (token.Length > 1 && token[0] == '0'))
        {
            throw new FormatException($"'{token}' is not a port between 1 and 65535.");
        }

        foreach (char character in token)
        {
            if (!char.IsAsciiDigit(character))
            {
                throw new FormatException($"'{token}' is not a port between 1 and 65535.");
            }
        }

        if (!ushort.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out ushort port) || port < MINIMUM)
        {
            throw new FormatException($"'{token}' is not a port between 1 and 65535.");
        }

        return port;
    }
}
