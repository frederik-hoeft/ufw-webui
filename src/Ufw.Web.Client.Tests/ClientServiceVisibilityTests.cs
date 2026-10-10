using System.Reflection;
using Ufw.Web.Client;
using Ufw.Web.Client.Features.KnownHosts;
using Ufw.Web.Client.Features.Rules.Metadata;
using Ufw.Web.Client.UI.Components.Rules.Filtering.Groups;
using Ufw.Web.Client.UI.Components.Rules.Filtering.Networks;
using Ufw.Web.Client.UI.Components.Rules.Filtering.Tags;

namespace Ufw.Web.Client.Tests;

[TestClass]
public sealed class ClientServiceVisibilityTests
{
    [TestMethod]
    public void ClientOwnedServiceImplementations_AreNotPublic()
    {
        Assembly assembly = typeof(Program).Assembly;
        Type[] implementations =
        [
            .. assembly.GetTypes().Where(type => type.IsClass && !type.IsAbstract && IsServiceNamespace(type)
                && type.GetInterfaces().Any(contract => contract.Assembly == assembly)),
        ];

        Assert.IsNotEmpty(implementations);
        Type[] exported = [.. implementations.Where(type => type.IsVisible)];
        Assert.IsEmpty(exported, $"Client service implementations must be internal: {string.Join(", ", exported.Select(type => type.FullName))}");
    }

    [TestMethod]
    public void FilterEditors_RetainPublicConstructorContracts()
    {
        AssertPublicConstructor(typeof(GroupRuleFilterEditor), typeof(IRuleGroupCatalogService));
        AssertPublicConstructor(typeof(TagRuleFilterEditor), typeof(IRuleTagCatalogService));
        AssertPublicConstructor(typeof(NetworkRuleFilterEditor), typeof(IKnownHostInventoryService));
    }

    private static void AssertPublicConstructor(Type componentType, Type contractType)
    {
        Assert.IsTrue(contractType.IsVisible, $"{contractType.FullName} must remain publicly accessible to its Razor component.");
        ConstructorInfo? constructor = componentType.GetConstructor([contractType]);
        Assert.IsNotNull(constructor, $"{componentType.FullName} should keep constructor injection for {contractType.Name}.");
    }

    private static bool IsServiceNamespace(Type type)
    {
        string? namespaceName = type.Namespace;
        return namespaceName is not null && (
            namespaceName.StartsWith("Ufw.Web.Client.Api", StringComparison.Ordinal) ||
            namespaceName.StartsWith("Ufw.Web.Client.Features", StringComparison.Ordinal) ||
            namespaceName.StartsWith("Ufw.Web.Client.Services", StringComparison.Ordinal));
    }
}
