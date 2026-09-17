using Ufw.Client.Components.Rules.Metadata;
using Ufw.Client.Rules.Metadata;

namespace Ufw.Client.Tests.Rules.Metadata;

[TestClass]
public sealed class RuleMetadataEditorResultTests
{
    [TestMethod]
    public void FromMetadata_AndNormalize_PreserveStableTagIdentityAndNormalizeNotes()
    {
        Guid firstTagId = Guid.CreateVersion7();
        Guid secondTagId = Guid.CreateVersion7();
        RuleMetadata metadata = new(
            Guid.CreateVersion7(),
            "  operator note  ",
            [new RuleTag(secondTagId, "two", "#112233"), new RuleTag(firstTagId, "one", "#445566")]);

        RuleMetadataEditorResult result = RuleMetadataEditorResult.FromMetadata(metadata).Normalize();

        Assert.AreEqual("operator note", result.Notes);
        CollectionAssert.AreEqual(new[] { firstTagId, secondTagId }.Order().ToArray(), result.TagIds.ToArray());
    }

    [TestMethod]
    public void Normalize_EmptyWhitespaceAndDuplicateTags_ProducesCanonicalDraft()
    {
        Guid tagId = Guid.CreateVersion7();
        RuleMetadataEditorResult result = new("   ", [tagId, tagId]);

        RuleMetadataEditorResult normalized = result.Normalize();

        Assert.IsNull(normalized.Notes);
        CollectionAssert.AreEqual(new[] { tagId }, normalized.TagIds.ToArray());
        Assert.IsFalse(normalized.IsEmpty);
        Assert.IsTrue(RuleMetadataEditorResult.Empty.IsEmpty);
    }
}
