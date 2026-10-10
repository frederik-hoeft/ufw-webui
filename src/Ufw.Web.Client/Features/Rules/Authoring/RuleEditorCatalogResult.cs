using Ufw.Web.Client.Services.Errors;

namespace Ufw.Web.Client.Features.Rules.Authoring;

/// <summary>
/// Carries one reference catalog's available suggestions and its independent load failure.
/// A failed lookup supplies no suggestions, but does not prevent another catalog from loading.
/// </summary>
internal sealed record RuleEditorCatalogResult<TItem>
{
    private RuleEditorCatalogResult(IReadOnlyList<TItem> items, ClientError? error)
    {
        Items = items;
        Error = error;
    }

    public IReadOnlyList<TItem> Items { get; }

    public ClientError? Error { get; }

    public static RuleEditorCatalogResult<TItem> Loaded(IReadOnlyList<TItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return new RuleEditorCatalogResult<TItem>(items, error: null);
    }

    public static RuleEditorCatalogResult<TItem> Failed(ClientError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new RuleEditorCatalogResult<TItem>([], error);
    }
}
