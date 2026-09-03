namespace Ufw.Systemd.Interop.IO;

internal sealed class ChildProcessException(string message, Exception innerException) : Exception(message, innerException);
