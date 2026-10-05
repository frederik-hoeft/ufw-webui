using Moq;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Systemd.Firewall;
using Ufw.Systemd.NetworkInterfaces;

namespace Ufw.Systemd.Tests.Firewall;

[TestClass]
public sealed class FirewallRuleInterfaceValidatorTests
{
    [TestMethod]
    public void Validate_MissingInterfaces_ReturnsStableValidationIdentityForEachField()
    {
        Mock<INetworkInterfaceSnapshotService> networkInterfaces = new(MockBehavior.Strict);
        networkInterfaces.Setup(static service => service.GetSnapshot()).Returns(NetworkInterfaceSnapshot.Available(["eth0"]));
        FirewallRuleInterfaceValidator validator = new(networkInterfaces.Object);
        FirewallRuleSpecification rule = new()
        {
            SourceInterface = "missing-source",
            DestinationInterface = "missing-destination",
        };

        IResponsePayload? response = validator.Validate(rule);

        ModelValidationErrorResponse validation = Assert.IsInstanceOfType<ModelValidationErrorResponse>(response);
        Assert.HasCount(2, validation.Errors);
        Assert.IsTrue(validation.Errors.Any(static error =>
            error.PropertyName == nameof(FirewallRuleSpecification.SourceInterface) && error.Code == FirewallRuleValidationErrorCodes.INTERFACE_NOT_FOUND));
        Assert.IsTrue(validation.Errors.Any(static error =>
            error.PropertyName == nameof(FirewallRuleSpecification.DestinationInterface) && error.Code == FirewallRuleValidationErrorCodes.INTERFACE_NOT_FOUND));
    }
}
