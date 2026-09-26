using Ufw.Web.Client.Features.Rules.Metadata;
using Ufw.Web.Client.UI.Components.Rules.Metadata;

namespace Ufw.Web.Client.Tests.UI.Components.Rules.Metadata;

[TestClass]
public sealed class RuleMetadataEditorResultTests
{
    [TestMethod]
    public void FromMetadata_AndNormalize_PreserveStableMetadataIdentitiesAndNormalizeNotes()
    {
        Guid firstTagId = Guid.CreateVersion7();
        Guid secondTagId = Guid.CreateVersion7();
        Guid groupId = Guid.CreateVersion7();
        RuleMetadata metadata = new(
            Guid.CreateVersion7(),
            "  operator note  ",
            [new RuleTag(secondTagId, "two", "#112233"), new RuleTag(firstTagId, "one", "#445566")],
            new RuleGroupMembership(groupId, "ops", "managed"));

        RuleMetadataEditorResult result = RuleMetadataEditorResult.FromMetadata(metadata).Normalize();

        Assert.AreEqual("operator note", result.Notes);
        CollectionAssert.AreEqual(new[] { firstTagId, secondTagId }.Order().ToArray(), result.TagIds.ToArray());
        Assert.AreEqual(groupId, result.GroupId);
    }

    [TestMethod]
    public void Normalize_EmptyWhitespaceAndDuplicateTags_ProducesCanonicalDraft()
    {
        Guid tagId = Guid.CreateVersion7();
        RuleMetadataEditorResult result = new("   ", [tagId, tagId], null);

        RuleMetadataEditorResult normalized = result.Normalize();

        Assert.IsNull(normalized.Notes);
        CollectionAssert.AreEqual(new[] { tagId }, normalized.TagIds.ToArray());
        Assert.IsNull(normalized.GroupId);
        Assert.IsFalse(normalized.IsEmpty);
        Assert.IsTrue(RuleMetadataEditorResult.Empty.IsEmpty);
    }

    [TestMethod]
    public void IsEmpty_GroupOnlyDraft_IsNotEmpty()
    {
        RuleMetadataEditorResult result = new(null, [], Guid.CreateVersion7());

        Assert.IsFalse(result.IsEmpty);
    }
}
