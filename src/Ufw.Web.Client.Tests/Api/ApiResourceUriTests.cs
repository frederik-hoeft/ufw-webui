using Ufw.Web.Client.Api;

namespace Ufw.Web.Client.Tests.Api;

[TestClass]
public sealed class ApiResourceUriTests
{
    private static readonly Guid s_id = Guid.Parse("01993b41-fdad-7000-8000-000000000002");

    [TestMethod]
    public void ForId_FormatsGuidAsRelativeResourcePath()
    {
        Uri uri = ApiResourceUri.ForId("api/v1/known-hosts", s_id, "hostId", "Known host");

        Assert.IsFalse(uri.IsAbsoluteUri);
        Assert.AreEqual($"api/v1/known-hosts/{s_id:D}", uri.OriginalString);
    }

    [TestMethod]
    public void ForId_AppendsNamedSubresourcesWithoutChangingTheId()
    {
        Uri uri = ApiResourceUri.ForId("api/v1/known-hosts/", s_id, "hostId", "Known host", "dns", "reconcile");

        Assert.AreEqual($"api/v1/known-hosts/{s_id:D}/dns/reconcile", uri.OriginalString);
    }

    [TestMethod]
    public void ForId_RejectsEmptyGuidWithTheCallersArgumentName()
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => ApiResourceUri.ForId("api/v1/rule-groups", Guid.Empty, "groupId", "Rule group"));

        Assert.AreEqual("groupId", exception.ParamName);
        StringAssert.Contains(exception.Message, "Rule group ID must not be empty.");
    }
}
