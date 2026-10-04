using Microsoft.AspNetCore.Mvc;
using Ufw.Web.Data;
using Wkg.AspNetCore.TestAdapters;

namespace Ufw.Web.Tests.Integration;

internal abstract class ControllerIntegrationTest<TController> : TransactionalControllerTest<TController, ApplicationDbContext, IntegrationTestInitializer>
    where TController : ControllerBase;
