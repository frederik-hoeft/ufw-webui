using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Ufw.Ipc.Shared.Model.Domain.Rules;

/// <summary>
/// Content-addressed identity of a firewall rule. Two rules with the same
/// identity are semantically the same match/action regardless of UFW numbering
/// or comment text.
/// </summary>
public static class RuleIdentity
{
    public const string PREFIX = "sha256:";

    public static string Compute(FirewallRuleSpecification specification)
    {
        ArgumentNullException.ThrowIfNull(specification);
        FirewallRuleSpecification normalized = RuleSpecificationNormalizer.Normalize(specification);
        string canonical = RuleSpecificationNormalizer.CanonicalizeIdentity(normalized);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return PREFIX + Base64Url.EncodeToString(hash);
    }

    public static bool AreEqual(FirewallRuleSpecification left, FirewallRuleSpecification right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return string.Equals(Compute(left), Compute(right), StringComparison.Ordinal);
    }
}
