using System.Buffers.Text;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Serialization.Json;
using Ufw.Shared.Security.Intent;
using Ufw.Web.Model.V1.Rules.Intent;

namespace Ufw.Web.Tests.Api.V1;

[TestClass]
public sealed class SignedRuleIntentRequestValidationTests
{
    [TestMethod]
    public void Validate_AllSupportedCanonicalSignedRequests_AreAccepted()
    {
        SignedRuleIntentRequest[] requests =
        [
            CreateAdd(),
            CreateInsert(),
            CreateReplace(),
            CreateReorder(),
            CreateBatchDelete(),
            CreateDelete(),
        ];

        foreach (SignedRuleIntentRequest request in requests)
        {
            List<ValidationResult> results = Validate(request);
            Assert.HasCount(0, results, request.GetType().Name);
        }
    }

    [TestMethod]
    public void Deserialize_MissingExplicitEnvelopeValue_IsRejectedRatherThanDefaulted()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
        JsonObject json = Assert.IsInstanceOfType<JsonObject>(JsonNode.Parse(JsonSerializer.Serialize(CreateAdd(), options)));
        Assert.IsTrue(json.Remove("version"));

        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<AddRuleIntentRequest>(json.ToJsonString(), options));
    }

    [TestMethod]
    public void Validate_WrongOperation_IsRejectedAtRestBoundary()
    {
        AddRuleIntentRequest request = CreateAdd() with { Operation = IntentOperations.DELETE_RULE };

        List<ValidationResult> results = Validate(request);

        Assert.IsTrue(results.Any(static result => result.MemberNames.Contains(nameof(SignedRuleIntentRequest.Operation), StringComparer.Ordinal)));
    }

    [TestMethod]
    public void Validate_MalformedEnvelopeValues_AreRejected()
    {
        AddRuleIntentRequest request = CreateAdd() with
        {
            DeploymentId = "not-base64url!",
            KeyId = "sha256:not-base64url!",
            Nonce = "short",
            Signature = "short",
        };

        List<ValidationResult> results = Validate(request);

        Assert.IsTrue(HasMember(results, nameof(SignedRuleIntentRequest.DeploymentId)));
        Assert.IsTrue(HasMember(results, nameof(SignedRuleIntentRequest.KeyId)));
        Assert.IsTrue(HasMember(results, nameof(SignedRuleIntentRequest.Nonce)));
        Assert.IsTrue(HasMember(results, nameof(SignedRuleIntentRequest.Signature)));
    }

    [TestMethod]
    public void Validate_NonObjectPayload_IsRejected()
    {
        AddRuleIntentRequest request = CreateAdd() with { Payload = JsonSerializer.SerializeToElement("not-an-object") };

        List<ValidationResult> results = Validate(request);

        Assert.IsTrue(HasMember(results, nameof(SignedRuleIntentRequest.Payload)));
        Assert.HasCount(1, results);
    }

    [TestMethod]
    public void Validate_UnknownTopLevelPayloadProperty_IsRejected()
    {
        JsonElement payload = ParseJson("""
            {
              "rule": {
                "action": "Allow",
                "addressFamily": "IPv4",
                "direction": "In",
                "protocol": "Tcp",
                "source": "any",
                "destination": "any",
                "destinationPorts": "22"
              },
              "unexpected": true
            }
            """);
        AddRuleIntentRequest request = CreateAdd() with { Payload = payload };

        List<ValidationResult> results = Validate(request);

        Assert.IsTrue(HasMember(results, nameof(SignedRuleIntentRequest.Payload)));
        StringAssert.Contains(results.Single().ErrorMessage!, "Unknown JSON property 'unexpected'");
    }

    [TestMethod]
    public void Validate_DuplicatePayloadProperty_IsRejected()
    {
        JsonElement payload = ParseJson("""
            {
              "rule": {
                "action": "Allow",
                "addressFamily": "IPv4",
                "direction": "In",
                "protocol": "Tcp",
                "source": "any",
                "destination": "any",
                "destinationPorts": "22"
              },
              "rule": {
                "action": "Allow",
                "addressFamily": "IPv4",
                "direction": "In",
                "protocol": "Tcp",
                "source": "any",
                "destination": "any",
                "destinationPorts": "443"
              }
            }
            """);
        AddRuleIntentRequest request = CreateAdd() with { Payload = payload };

        List<ValidationResult> results = Validate(request);

        Assert.IsTrue(HasMember(results, nameof(SignedRuleIntentRequest.Payload)));
        StringAssert.Contains(results.Single().ErrorMessage!, "Duplicate JSON property 'rule'");
    }

    [TestMethod]
    public void Validate_MissingDefaultValuedPayloadProperty_IsRejectedRatherThanDefaulted()
    {
        JsonElement payload = ParseJson("""
            {
              "baselineFingerprint": "PLACEHOLDER",
              "rule": {
                "action": "Allow",
                "addressFamily": "IPv4",
                "direction": "In",
                "protocol": "Tcp",
                "source": "any",
                "destination": "any",
                "destinationPorts": "22"
              }
            }
            """.Replace("PLACEHOLDER", Fingerprint(), StringComparison.Ordinal));
        InsertRuleIntentRequest request = CreateInsert() with { Payload = payload };

        List<ValidationResult> results = Validate(request);

        Assert.IsTrue(HasMember(results, nameof(SignedRuleIntentRequest.Payload)));
        StringAssert.Contains(results.Single().ErrorMessage!, "Required JSON property 'anchorOccurrenceId' is missing");
    }

    [TestMethod]
    public void Validate_MissingCanonicalRuleProperty_IsRejectedRatherThanDefaulted()
    {
        JsonElement payload = ParseJson("""
            {
              "rule": {
                "addressFamily": "IPv4",
                "direction": "In",
                "protocol": "Tcp",
                "source": "any",
                "destination": "any",
                "destinationPorts": "22"
              }
            }
            """);
        AddRuleIntentRequest request = CreateAdd() with { Payload = payload };

        List<ValidationResult> results = Validate(request);

        Assert.IsTrue(HasMember(results, nameof(SignedRuleIntentRequest.Payload)));
        StringAssert.Contains(results.Single().ErrorMessage!, "Required JSON property 'action' is missing");
    }

    [TestMethod]
    public void Validate_UnknownNestedRuleProperty_IsRejected()
    {
        JsonElement payload = ParseJson("""
            {
              "rule": {
                "action": "Allow",
                "addressFamily": "IPv4",
                "direction": "In",
                "protocol": "Tcp",
                "source": "any",
                "destination": "any",
                "destinationPorts": "22",
                "unexpected": true
              }
            }
            """);
        AddRuleIntentRequest request = CreateAdd() with { Payload = payload };

        List<ValidationResult> results = Validate(request);

        Assert.IsTrue(HasMember(results, nameof(SignedRuleIntentRequest.Payload)));
        StringAssert.Contains(results.Single().ErrorMessage!, "Unknown JSON property 'unexpected'");
    }

    [TestMethod]
    public void Validate_NonCanonicalSignedRule_IsRejectedWithoutChangingPayload()
    {
        JsonElement payload = ParseJson("""
            {
              "rule": {
                "action": "Allow",
                "addressFamily": "IPv4",
                "direction": "In",
                "protocol": "Tcp",
                "source": "any",
                "destination": "any",
                "destinationPorts": " 22 "
              }
            }
            """);
        string originalJson = payload.GetRawText();
        AddRuleIntentRequest request = CreateAdd() with { Payload = payload };

        List<ValidationResult> results = Validate(request);

        Assert.IsTrue(results.Any(static result => result.MemberNames.Contains("Payload.Rule.DestinationPorts", StringComparer.Ordinal)));
        Assert.AreEqual(originalJson, request.Payload.GetRawText());
    }

    [TestMethod]
    public void Validate_SemanticallyInvalidSignedRule_IsRejected()
    {
        FirewallRuleSpecification invalidRule = CanonicalRule();
        invalidRule.DestinationPorts = "0";
        AddRuleIntentRequest request = CreateAdd() with
        {
            Payload = JsonSerializer.SerializeToElement(new AddRulePayload { Rule = invalidRule }, MessageJsonSerializerContext.Default.AddRulePayload),
        };

        List<ValidationResult> results = Validate(request);

        Assert.IsTrue(results.Any(static result => result.MemberNames.Contains("Payload.Rule.DestinationPorts", StringComparer.Ordinal)));
    }

    private static List<ValidationResult> Validate(SignedRuleIntentRequest request)
    {
        List<ValidationResult> results = [];
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results;
    }

    private static bool HasMember(IEnumerable<ValidationResult> results, string memberName) =>
        results.Any(result => result.MemberNames.Contains(memberName, StringComparer.Ordinal));

    private static AddRuleIntentRequest CreateAdd() => new()
    {
        Version = IntentProtocol.VERSION,
        DeploymentId = DeploymentId(),
        KeyId = KeyId(),
        IssuedAtUnix = 1_700_000_000,
        Nonce = Nonce(),
        Operation = IntentOperations.ADD_RULE,
        Payload = JsonSerializer.SerializeToElement(new AddRulePayload { Rule = CanonicalRule() }, MessageJsonSerializerContext.Default.AddRulePayload),
        Signature = Signature(),
    };

    private static InsertRuleIntentRequest CreateInsert() => new()
    {
        Version = IntentProtocol.VERSION,
        DeploymentId = DeploymentId(),
        KeyId = KeyId(),
        IssuedAtUnix = 1_700_000_000,
        Nonce = Nonce(),
        Operation = IntentOperations.INSERT_RULE,
        Payload = JsonSerializer.SerializeToElement(new InsertRulePayload
        {
            BaselineFingerprint = Fingerprint(),
            AnchorOccurrenceId = 0,
            Placement = RuleInsertionPlacement.Before,
            Rule = CanonicalRule(),
        }, MessageJsonSerializerContext.Default.InsertRulePayload),
        Signature = Signature(),
    };

    private static ReplaceRuleIntentRequest CreateReplace() => new()
    {
        Version = IntentProtocol.VERSION,
        DeploymentId = DeploymentId(),
        KeyId = KeyId(),
        IssuedAtUnix = 1_700_000_000,
        Nonce = Nonce(),
        Operation = IntentOperations.REPLACE_RULE,
        Payload = JsonSerializer.SerializeToElement(new ReplaceRulePayload
        {
            BaselineFingerprint = Fingerprint(),
            TargetOccurrenceId = 0,
            OriginalRuleId = RuleIdentity.Compute(CanonicalRule()),
            ReplacementRule = CanonicalRule(),
        }, MessageJsonSerializerContext.Default.ReplaceRulePayload),
        Signature = Signature(),
    };

    private static ReorderRulesIntentRequest CreateReorder() => new()
    {
        Version = IntentProtocol.VERSION,
        DeploymentId = DeploymentId(),
        KeyId = KeyId(),
        IssuedAtUnix = 1_700_000_000,
        Nonce = Nonce(),
        Operation = IntentOperations.REORDER_RULES,
        Payload = JsonSerializer.SerializeToElement(new ReorderRulesPayload
        {
            BaselineFingerprint = Fingerprint(),
            DesiredOrder = [0, 1],
        }, MessageJsonSerializerContext.Default.ReorderRulesPayload),
        Signature = Signature(),
    };

    private static BatchDeleteRulesIntentRequest CreateBatchDelete() => new()
    {
        Version = IntentProtocol.VERSION,
        DeploymentId = DeploymentId(),
        KeyId = KeyId(),
        IssuedAtUnix = 1_700_000_000,
        Nonce = Nonce(),
        Operation = IntentOperations.DELETE_RULES_BATCH,
        Payload = JsonSerializer.SerializeToElement(new BatchDeleteRulesPayload
        {
            BaselineFingerprint = Fingerprint(),
            OccurrenceIds = [0, 1],
        }, MessageJsonSerializerContext.Default.BatchDeleteRulesPayload),
        Signature = Signature(),
    };

    private static DeleteRuleIntentRequest CreateDelete()
    {
        FirewallRuleSpecification rule = CanonicalRule();
        return new DeleteRuleIntentRequest
        {
            Version = IntentProtocol.VERSION,
            DeploymentId = DeploymentId(),
            KeyId = KeyId(),
            IssuedAtUnix = 1_700_000_000,
            Nonce = Nonce(),
            Operation = IntentOperations.DELETE_RULE,
            Payload = JsonSerializer.SerializeToElement(new DeleteRulePayload
            {
                RuleId = RuleIdentity.Compute(rule),
                Rule = rule,
            }, MessageJsonSerializerContext.Default.DeleteRulePayload),
            Signature = Signature(),
        };
    }

    private static string DeploymentId() => Base64Url.EncodeToString(new byte[IntentProtocol.DEPLOYMENT_ID_SIZE_BYTES]);

    private static string KeyId() => IntentProtocol.KEY_ID_PREFIX + Base64Url.EncodeToString(new byte[SHA256.HashSizeInBytes]);

    private static string Nonce() => Base64Url.EncodeToString(new byte[IntentProtocol.NONCE_SIZE_BYTES]);

    private static string Signature() => Base64Url.EncodeToString(new byte[64]);

    private static FirewallRuleSpecification CanonicalRule() => new()
    {
        Action = FirewallAction.Allow,
        AddressFamily = FirewallAddressFamily.IPv4,
        Direction = FirewallDirection.In,
        Protocol = FirewallProtocol.Tcp,
        Source = RuleSpecificationNormalizer.ANY,
        Destination = RuleSpecificationNormalizer.ANY,
        DestinationPorts = "22",
        Comment = "ssh",
    };

    private static string Fingerprint() => FirewallRuleSnapshotFingerprint.Compute(active: true, []);

    private static JsonElement ParseJson(string json) => JsonSerializer.Deserialize<JsonElement>(json);
}
