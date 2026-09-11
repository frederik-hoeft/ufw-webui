using Wkg.AspNetCore.TestAdapters;

namespace Ufw.Web.Tests.Integration;

public abstract class ComponentIntegrationTest<TComponent> : ComponentTest<TComponent, IntegrationTestInitializer>
    where TComponent : class;
