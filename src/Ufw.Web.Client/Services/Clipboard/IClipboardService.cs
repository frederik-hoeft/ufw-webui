namespace Ufw.Web.Client.Services.Clipboard;

internal interface IClipboardService
{
    ValueTask WriteTextAsync(string value, CancellationToken cancellationToken = default);
}
