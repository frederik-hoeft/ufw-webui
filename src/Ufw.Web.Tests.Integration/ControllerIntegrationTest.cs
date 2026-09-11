using Microsoft.AspNetCore.Mvc;
using Ufw.Web.Data;
using Wkg.AspNetCore.TestAdapters;

namespace Ufw.Web.Tests.Integration;

public abstract class ControllerIntegrationTest<TController> : TransactionalControllerTest<TController, ApplicationDbContext, IntegrationTestInitializer>
    where TController : ControllerBase;
