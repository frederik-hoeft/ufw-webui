using ConsoleAppFramework;
using Ufw.Systemd;
try
{
    ConsoleApp.ConsoleAppBuilder app = ConsoleApp.Create();
    app.Add<Commands>();
    await app.RunAsync(args);
}
catch (Exception)
{
    Console.ReadLine();
    throw;
}