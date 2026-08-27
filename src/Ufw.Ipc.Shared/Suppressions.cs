namespace Ufw.Ipc.Shared;

public static class Suppressions
{
    public const string CA2000_WARN_OBJECT_NOT_DISPOSED = "CA2000:Dispose objects before losing scope";
    public const string CA2000_OWNERSHIP_TRANSFER = "The ownership of the disposable object is transferred to the caller.";
    public const string CA2000_DISPOSED_BY_PROXY = "The object is indirectly disposed by through a nested method call.";
    public const string IDE1006_WARN_NAMING = "IDE1006:Naming Styles";
    public const string IDE1006_INTEROP_NAMING = "In interop code, the names of types, members, and parameters should match the target platform's conventions.";
    public const string IDE1006_THREAD_STATIC_NAMING = "Thread-static fields should be named using the 't_' prefix.";
}
