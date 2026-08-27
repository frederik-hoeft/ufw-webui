using ConsoleAppFramework;
using Ufw.Systemd;

ConsoleApp.ConsoleAppBuilder app = ConsoleApp.Create();
app.Add<Commands>();
await app.RunAsync(args);
