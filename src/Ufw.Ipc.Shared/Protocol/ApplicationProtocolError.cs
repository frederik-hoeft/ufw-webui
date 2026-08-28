namespace Ufw.Ipc.Shared.Protocol;

public enum ApplicationProtocolError
{
    None = 0,
    EmptyDocument = 1,
    InvalidJson,
    VersionMismatch,
    InvalidKind,
    MissingRequiredField,
    UnknownPayloadType,
    PayloadTypeMismatch,
    InvalidStatus,
    UnexpectedField,
    PayloadDeserializeFailed,
}
