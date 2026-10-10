using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ufw.Web.Client.Services.Localization;
using Ufw.Web.Client.Tests.Support;
using Ufw.Web.Client.UI.Components.Standalone;

namespace Ufw.Web.Client.Tests.UI.Components.Standalone;

[TestClass]
public sealed class StandaloneErrorDiagnosticTests
{
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public async Task Render_NoDiagnosticReference_HidesDetailsAsync(string? reference)
    {
        string html = await RenderAsync(reference);

        Assert.AreEqual(string.Empty, html);
    }

    [TestMethod]
    public async Task Render_DiagnosticReference_EncodesValueAndPreservesStyleAsync()
    {
        string html = await RenderAsync("error <123> & debug");

        StringAssert.Contains(html, "error-diagnostic-reference");
        StringAssert.Contains(html, "DiagnosticReference:");
        StringAssert.Contains(html, "error &lt;123&gt; &amp; debug");
        Assert.IsFalse(html.Contains("error <123>", StringComparison.Ordinal));
    }

    private static async Task<string> RenderAsync(string? reference)
    {
        ServiceCollection services = new();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton<IStringLocalizer<ErrorsStrings>, PassthroughStringLocalizer<ErrorsStrings>>();
        using ServiceProvider provider = services.BuildServiceProvider();
        await using HtmlRenderer renderer = new(provider, NullLoggerFactory.Instance);

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            HtmlRootComponent component = await renderer.RenderComponentAsync<StandaloneErrorDiagnostic>(
                ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(StandaloneErrorDiagnostic.DiagnosticReference)] = reference }));
            return component.ToHtmlString();
        });
    }
}
