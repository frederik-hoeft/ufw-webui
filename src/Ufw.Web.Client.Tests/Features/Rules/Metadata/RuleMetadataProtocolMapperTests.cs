using Ufw.Shared.Management.Rules;
using Ufw.Web.Client.Api;
using Ufw.Web.Client.Features.Rules.Metadata;

namespace Ufw.Web.Client.Tests.Features.Rules.Metadata;

[TestClass]
public sealed class RuleMetadataProtocolMapperTests
{
    [TestMethod]
    public void MapTag_ValidTag_NormalizesNameAndColor()
    {
        Guid id = Guid.CreateVersion7();
        RuleTagItem item = new() { Id = id, Name = "  production  ", Color = " #aabbcc " };

        RuleTag tag = RuleMetadataProtocolMapper.MapTag(item, "invalid tag");

        Assert.AreEqual(id, tag.Id);
        Assert.AreEqual("production", tag.Name);
        Assert.AreEqual("#AABBCC", tag.Color);
    }

    [TestMethod]
    public void MapTag_MalformedIdentityNameOrColor_RejectsConsistently()
    {
        RuleTagItem[] invalid =
        [
            new() { Id = Guid.Empty, Name = "production", Color = "#112233" },
            new() { Id = Guid.CreateVersion7(), Name = "  ", Color = "#112233" },
            new() { Id = Guid.CreateVersion7(), Name = "production", Color = "red" },
        ];
        foreach (RuleTagItem tag in invalid)
        {
            Assert.ThrowsExactly<ApiProtocolException>(() => RuleMetadataProtocolMapper.MapTag(tag, "invalid tag"));
            RuleMetadataItem item = new() { Id = Guid.CreateVersion7(), RuleId = "rule", Tags = [tag] };
            Assert.ThrowsExactly<ApiProtocolException>(() => RuleMetadataProtocolMapper.MapMetadata(item, "invalid metadata"));
        }
    }

    [TestMethod]
    public void MapGroupReference_OptionalOrPopulated_NormalizesAndValidates()
    {
        Assert.IsNull(RuleMetadataProtocolMapper.MapGroupReference(null, "invalid group"));

        Guid id = Guid.CreateVersion7();
        RuleGroupMembership? mapped = RuleMetadataProtocolMapper.MapGroupReference(new RuleGroupSummary(id, " production ", "  note  "), "invalid group");
        Assert.IsNotNull(mapped);
        Assert.AreEqual(id, mapped.Id);
        Assert.AreEqual("production", mapped.Name);
        Assert.AreEqual("note", mapped.Comment);
        Assert.ThrowsExactly<ApiProtocolException>(() => RuleMetadataProtocolMapper.MapGroupReference(new RuleGroupSummary(Guid.Empty, "name", null), "invalid group"));
    }

    [TestMethod]
    public void MapMetadata_DuplicateTagIdentityRejectedButNamePolicyRemainsWithCaller()
    {
        Guid tagId = Guid.CreateVersion7();
        RuleMetadataItem invalid = new()
        {
            Id = Guid.CreateVersion7(),
            RuleId = "rule",
            Tags =
            [
                new RuleTagItem { Id = tagId, Name = "one", Color = "#111111" },
                new RuleTagItem { Id = tagId, Name = "two", Color = "#222222" },
            ],
        };
        Assert.ThrowsExactly<ApiProtocolException>(() => RuleMetadataProtocolMapper.MapMetadata(invalid, "duplicate tag"));

        RuleMetadataItem differentTagsSameName = new()
        {
            Id = Guid.CreateVersion7(),
            RuleId = "rule",
            Tags =
            [
                new RuleTagItem { Id = Guid.CreateVersion7(), Name = "one", Color = "#111111" },
                new RuleTagItem { Id = Guid.CreateVersion7(), Name = "one", Color = "#222222" },
            ],
        };
        RuleMetadata metadata = RuleMetadataProtocolMapper.MapMetadata(differentTagsSameName, "invalid metadata");
        Assert.HasCount(2, metadata.Tags);
    }
}
