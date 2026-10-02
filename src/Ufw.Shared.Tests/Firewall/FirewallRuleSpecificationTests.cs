using System.Reflection;
using Ufw.Shared.Firewall;

namespace Ufw.Shared.Tests.Firewall;

[TestClass]
public sealed class FirewallRuleSpecificationTests
{
    [TestMethod]
    public void CopyWithAddressFamily_CopiesSpecificationWithoutMutatingSource()
    {
        FirewallRuleSpecification source = new()
        {
            Action = FirewallAction.Deny,
            AddressFamily = FirewallAddressFamily.Any,
            Direction = FirewallDirection.Forward,
            Protocol = FirewallProtocol.Udp,
            Source = "10.0.0.0/8",
            SourcePorts = "53",
            SourceInterface = "eth0",
            Destination = "192.0.2.0/24",
            DestinationPorts = "1000:2000",
            DestinationInterface = "eth1",
            Comment = "copied rule",
        };

        FirewallRuleSpecification copy = source.CopyWithAddressFamily(FirewallAddressFamily.IPv6);

        Assert.AreNotSame(source, copy);
        Assert.AreEqual(FirewallAddressFamily.Any, source.AddressFamily);
        Assert.AreEqual(FirewallAddressFamily.IPv6, copy.AddressFamily);

        foreach (PropertyInfo property in typeof(FirewallRuleSpecification).GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.Name == nameof(FirewallRuleSpecification.AddressFamily))
            {
                continue;
            }

            Assert.AreEqual(property.GetValue(source), property.GetValue(copy), $"Property '{property.Name}' was not preserved by the copy.");
        }
    }
}
