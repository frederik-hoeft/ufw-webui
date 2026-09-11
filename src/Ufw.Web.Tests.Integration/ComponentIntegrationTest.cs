using Ufw.Web.Data;
using Wkg.AspNetCore.TestAdapters;

namespace Ufw.Web.Tests.Integration;

public abstract class ComponentIntegrationTest<TComponent> : TransactionalComponentTest<TComponent, ApplicationDbContext, IntegrationTestInitializer>
    where TComponent : class;
