using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Ufw.Shared.Domain.Algebra;
using Ufw.Shared.Extensions;

namespace Ufw.Shared.Domain;

/// <summary>
/// Numeric port domain used by protocols with port semantics. The closed world is <c>1..65535</c>.
/// Null, blank, and <c>any</c> parse as that whole numeric domain; protocols without ports are represented separately.
/// </summary>
public static class PacketPorts
{
    /// <summary>Gets the smallest modeled port.</summary>
    public const ushort MINIMUM = 1;

    /// <summary>Gets the largest modeled port.</summary>
    public const ushort MAXIMUM = 65535;

    /// <summary>Gets every modeled numeric port.</summary>
    public static IntervalSet<ushort> Universe { get; } = IntervalSet<ushort>.Between(MINIMUM, MAXIMUM);

    /// <summary>
    /// Parses a comma-separated list of ports and inclusive <c>start:end</c> ranges.
    /// Whitespace around entries and range delimiters is ignored.
    /// </summary>
    public static IntervalSet<ushort> Parse(ReadOnlySpan<char> text) => TryParseCore(text, out IntervalSet<ushort> ports, out ReadOnlySpan<char> errorSegment, out ParseError error)
        ? ports
        : ThrowParseError(errorSegment, error);

    [DoesNotReturn]
    private static IntervalSet<ushort> ThrowParseError(ReadOnlySpan<char> errorSegment, ParseError error) => error switch
    {
        ParseError.None => throw new FormatException($"'{errorSegment}' is not a valid port segment."),
        ParseError.EmptySegment => throw new FormatException($"'{errorSegment}' is an empty port segment."),
        ParseError.InvalidPort => throw new FormatException($"'{errorSegment}' is not a valid port."),
        ParseError.InvalidRange => throw new FormatException($"'{errorSegment}' is not a valid port range."),
        ParseError.DescendingRange => throw new FormatException($"'{errorSegment}' has a descending port range."),
        _ => throw new FormatException($"'{errorSegment}' is not a valid port segment: {error}.")
    };

    /// <summary>
    /// Parses a comma-separated list of ports and inclusive <c>start:end</c> ranges.
    /// Whitespace around entries and range delimiters is ignored.
    /// </summary>
    public static IntervalSet<ushort> Parse(string? text) => text is null ? Parse([]) : Parse(text.AsSpan());

    /// <summary>
    /// Tries to parse comma-separated ports and inclusive <c>start:end</c> ranges, allowing whitespace around entries and delimiters.
    /// Returns <c>false</c> for invalid entries without throwing a format exception.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="ports">The parsed ports, if parsing succeeded.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<char> text, out IntervalSet<ushort> ports) =>
        TryParseCore(text, out ports, out _, out _);

    /// <summary>
    /// Tries to parse comma-separated ports and inclusive <c>start:end</c> ranges, allowing whitespace around entries and delimiters.
    /// Returns <c>false</c> for invalid entries without throwing a format exception.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="ports">The parsed ports, if parsing succeeded.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(string? text, out IntervalSet<ushort> ports) => text is null
        ? TryParse([], out ports)
        : TryParse(text.AsSpan(), out ports);

    private static bool TryParseCore(ReadOnlySpan<char> text, out IntervalSet<ushort> ports, out ReadOnlySpan<char> errorSegment, out ParseError error)
    {
        ReadOnlySpan<char> trimmed = text.Trim();
        if (text.IsWhiteSpace() || trimmed.Equals("any", StringComparison.OrdinalIgnoreCase))
        {
            ports = Universe;
            return true.WithDefaults(out errorSegment, out error);
        }
        int separators = trimmed.Count(',');
        if (separators == 0)
        {
            // single segment
            if (TryParseSegment(trimmed, out Interval<ushort> range, out errorSegment, out error))
            {
                ports = IntervalSet<ushort>.Of(range);
                return true.WithDefaults(out errorSegment, out error);
            }
            return false.WithDefaults(out ports);
        }
        List<Interval<ushort>> ranges = new(capacity: separators + 1);
        ReadOnlySpan<char> remaining = trimmed;
        while (true)
        {
            int segmentEnd = remaining.IndexOf(',');
            ReadOnlySpan<char> segment = segmentEnd < 0 ? remaining : remaining[..segmentEnd].TrimEnd();
            if (segment.IsEmpty)
            {
                errorSegment = segment;
                error = ParseError.EmptySegment;
                return false.WithDefaults(out ports);
            }
            if (!TryParseSegment(segment, out Interval<ushort> range, out errorSegment, out error))
            {
                return false.WithDefaults(out ports);
            }

            ranges.Add(range);
            if (segmentEnd < 0)
            {
                break;
            }
            remaining = remaining[(segmentEnd + 1)..].TrimStart();
        }

        ports = IntervalSet<ushort>.Of(ranges);
        return true.WithDefaults(out errorSegment, out error);
    }

    private static bool TryParseSegment(ReadOnlySpan<char> segment, out Interval<ushort> range, out ReadOnlySpan<char> errorSegment, out ParseError error)
    {
        int colon = segment.IndexOf(':');
        if (colon == -1)
        {
            if (TryParsePort(segment, out ushort port))
            {
                range = new Interval<ushort>(port, port);
                return true.WithDefaults(out errorSegment, out error);
            }
            errorSegment = segment;
            error = ParseError.InvalidPort;
            return false.WithDefaults(out range);
        }
        if (colon is 0 || colon == segment.Length - 1 || colon != segment.LastIndexOf(':'))
        {
            errorSegment = segment;
            error = ParseError.InvalidRange;
            return false.WithDefaults(out range);
        }
        ReadOnlySpan<char> startSegment = segment[..colon].TrimEnd();
        if (!TryParsePort(startSegment, out ushort startPort))
        {
            errorSegment = startSegment;
            error = ParseError.InvalidPort;
            return false.WithDefaults(out range);
        }
        ReadOnlySpan<char> endSegment = segment[(colon + 1)..].TrimStart();
        if (!TryParsePort(endSegment, out ushort endPort))
        {
            errorSegment = endSegment;
            error = ParseError.InvalidPort;
            return false.WithDefaults(out range);
        }
        if (startPort > endPort)
        {
            errorSegment = segment;
            error = ParseError.DescendingRange;
            return false.WithDefaults(out range);
        }
        range = new Interval<ushort>(startPort, endPort);
        return true.WithDefaults(out errorSegment, out error);
    }

    private static bool TryParsePort(ReadOnlySpan<char> token, out ushort port)
    {
        if (token.Length is < 1 or > 5 || token[0] == '0')
        {
            return false.WithDefaults(out port);
        }
        if (!ushort.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out ushort result) || result < MINIMUM)
        {
            return false.WithDefaults(out port);
        }
        port = result;
        return true;
    }

    private enum ParseError
    {
        None,
        EmptySegment,
        InvalidPort,
        InvalidRange,
        DescendingRange
    }
}
