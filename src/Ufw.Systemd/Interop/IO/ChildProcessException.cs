using System.Diagnostics.CodeAnalysis;

namespace Ufw.Systemd.Interop.IO;

[SuppressMessage("Design", "CA1032:Implement standard exception constructors", Justification = "Designed to be thrown with a specific error code and message.")]
internal sealed class ChildProcessException(string message, Exception innerException) : Exception(message, innerException);
