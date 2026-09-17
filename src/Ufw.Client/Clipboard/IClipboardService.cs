namespace Ufw.Client.Clipboard;

internal interface IClipboardService
{
    ValueTask WriteTextAsync(string value, CancellationToken cancellationToken = default);
}
