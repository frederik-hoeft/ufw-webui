namespace Ufw.Web.Client.Services.Storage;

internal interface ILocalStorage
{
    ValueTask<string?> GetItemAsync(string key, CancellationToken cancellationToken = default);

    ValueTask SetItemAsync(string key, string value, CancellationToken cancellationToken = default);

    ValueTask RemoveItemAsync(string key, CancellationToken cancellationToken = default);
}
