using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Web.Client.Features.Rules.Authoring;
using Ufw.Web.Client.Services.Localization;
using Ufw.Web.Client.Tests.Support;

namespace Ufw.Web.Client.Tests.Features.Rules.Authoring;

[TestClass]
public sealed class RuleValidationMessageLocalizerTests
{
    [TestMethod]
    public void Localize_KnownMessageUsesResourceAndUnknownMessagePassesThrough()
    {
        RuleValidationMessageLocalizer localizer = new(new PassthroughStringLocalizer<ValidationStrings>());

        string known = localizer.Localize(new ModelValidationError("Action", "Action is not supported."));
        string ipv6Disabled = localizer.Localize(new ModelValidationError("AddressFamily", "IPv6 rules are unavailable because IPv6 support is disabled in the current UFW configuration."));
        string unknown = localizer.Localize(new ModelValidationError("Custom", "Daemon-specific validation detail."));

        Assert.AreEqual("ActionUnsupported", known);
        Assert.AreEqual("Ipv6Disabled", ipv6Disabled);
        Assert.AreEqual("Daemon-specific validation detail.", unknown);
    }
}
