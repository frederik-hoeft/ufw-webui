using System.Collections.Frozen;

namespace Ufw.Ipc.Shared.Protocol;

/// <summary>
/// Well-known application-level payload representation identifiers. Shared status codes
/// (for example HTTP 400) are disambiguated by this field, not by probing DTOs.
/// </summary>
public static class ApplicationPayloadTypes
{
    public const string EMPTY = "empty";
    public const string DATA = "data";
    public const string ERROR = "error";
    public const string VALIDATION_ERROR = "validation-error";

    private static readonly FrozenSet<string> s_known = new[] { EMPTY, DATA, ERROR, VALIDATION_ERROR }.ToFrozenSet(StringComparer.Ordinal);

    public static bool IsKnown(string? payloadType) => payloadType is not null && s_known.Contains(payloadType);

    public static bool IsRequestPayloadType(string? payloadType) =>
        payloadType is EMPTY or DATA;

    public static bool IsResponsePayloadType(int statusCode, string? payloadType) =>
        statusCode >= 400
            ? payloadType is ERROR or VALIDATION_ERROR
            : payloadType is EMPTY or DATA;
}
