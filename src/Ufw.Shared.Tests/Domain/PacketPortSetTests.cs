using Ufw.Shared.Domain;

namespace Ufw.Shared.Tests.Domain;

[TestClass]
public sealed class PacketPortSetTests
{
    [TestMethod]
    public void Algebra_DistinguishesNumericPortsFromNotApplicable()
    {
        PacketPortSet web = PacketPortSet.FromPorts(PacketPorts.Parse("80:81"));
        PacketPortSet mixed = web.Union(PacketPortSet.NotApplicable);

        Assert.IsTrue(mixed.Contains(80));
        Assert.IsTrue(mixed.Contains(81));
        Assert.IsTrue(mixed.Contains(null));
        Assert.AreEqual(web, mixed.Except(PacketPortSet.NotApplicable));
        Assert.AreEqual(PacketPortSet.NotApplicable, mixed.Except(web));
        Assert.AreEqual(PacketPortSet.Empty, web.Intersect(PacketPortSet.NotApplicable));
    }
}
