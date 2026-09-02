namespace Ufw.Systemd.Configuration.Model;

internal sealed class AppSettings : IRequireValidation
{
    public bool DebugMode { get; set; }

    public string UfwPath { get; init; } = "/usr/sbin/ufw";

    public bool WriteToConsole { get; set; }

    public required PipeOptions Pipe { get; set; }

    public required NetworkOptions Network { get; set; }

    public SecurityOptions? Security { get; set; }

    public bool AssertIsValid() => _ = this is
    {
        UfwPath.Length: > 0,
        Pipe: not null,
    } && File.Exists(UfwPath) && Pipe.AssertIsValid() && Network.AssertIsValid()
        && Security?.AssertIsValid() is not false
        ? true : throw new InvalidOperationException("invalid configuration");
}
