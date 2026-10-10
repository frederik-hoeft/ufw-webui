using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ufw.Web.Client.Services.Localization;
using Ufw.Web.Client.Tests.Support;
using Ufw.Web.Client.UI.Components.Rules;

namespace Ufw.Web.Client.Tests.UI.Components.Rules;

[TestClass]
public sealed class RuleRowReadOnlyContentTests
{
    [TestMethod]
    public async Task ReadOnlyContent_RendersSharedLabelsWithoutAddingLayoutElements()
    {
        ServiceCollection services = new();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton<IStringLocalizer<RulesStrings>, PassthroughStringLocalizer<RulesStrings>>();
        using ServiceProvider provider = services.BuildServiceProvider();
        await using HtmlRenderer renderer = new(provider, NullLoggerFactory.Instance);

        const string rawLine = "deny <unknown> & more";
        string html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            HtmlRootComponent component = await renderer.RenderComponentAsync<RuleRowReadOnlyContent>(
                ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(RuleRowReadOnlyContent.RawLine)] = rawLine }));
            return component.ToHtmlString();
        });

        Assert.AreEqual("<span class=\"rule-unsupported-label\">ReadOnly</span>\n<span class=\"rule-raw-line\">deny &lt;unknown&gt; &amp; more</span>", html);
    }
}
