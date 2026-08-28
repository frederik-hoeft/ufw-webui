using System.Collections.Frozen;

namespace Ufw.Ipc.Shared.Protocol;

/// <summary>
/// Well-known application-level payload discriminators. Shared status codes
/// (for example HTTP 400) are disambiguated by this field, not by probing DTOs.
/// </summary>
public static class ApplicationPayloadTypes
{
    public const string Empty = "empty";
    public const string Data = "data";
    public const string Error = "error";
    public const string ValidationError = "validation-error";

    private static readonly FrozenSet<string> s_known = new[] { Empty, Data, Error, ValidationError }.ToFrozenSet(StringComparer.Ordinal);

    public static bool IsKnown(string? payloadType) => payloadType is not null && s_known.Contains(payloadType);
}
