namespace Ufw.Systemd.Configuration.Model;

internal sealed class AppSettings : IRequireValidation
{
    public bool DebugMode { get; set; }

    public string UfwPath { get; init; } = "/usr/sbin/ufw";

    public bool WriteToConsole { get; set; }

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public void AssertIsValid() => _ = this is
    {
        UfwPath.Length: > 0,
    } && File.Exists(UfwPath) ? true : throw new InvalidOperationException("invalid configuration");
}