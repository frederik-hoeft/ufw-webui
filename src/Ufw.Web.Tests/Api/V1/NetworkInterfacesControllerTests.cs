using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Data;
using Ufw.Ipc.Client;
using Ufw.Web.Api.V1.Controllers;
using Ufw.Web.Api.V1.Models.NetworkInterfaces;
using Ufw.Web.Data;
using Ufw.Web.Services.NetworkInterfaces;
using Wkg.AspNetCore.Transactions;
using Wkg.AspNetCore.Transactions.Configuration;
using Wkg.EntityFrameworkCore.Configuration;

namespace Ufw.Web.Tests.Api.V1;

[TestClass]
public sealed class NetworkInterfacesControllerTests
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task GetAsync_ReturnsCachedInventoryWithoutReconcilingAsync()
    {
        await using ControllerTestHost host = await ControllerTestHost.CreateAsync(TestContext.CancellationToken);
        NetworkInterfaceInventoryResponse expected = new(
            [new NetworkInterfaceInventoryItem(Guid.CreateVersion7(), "eno1", "service VLAN")],
            new DateTimeOffset(2026, 9, 8, 20, 0, 0, TimeSpan.Zero));
        host.Inventory.Setup(service => service.GetCachedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        ActionResult<NetworkInterfaceInventoryResponse> result = await host.Controller.GetAsync(TestContext.CancellationToken);

        OkObjectResult ok = Assert.IsInstanceOfType<OkObjectResult>(result.Result);
        Assert.AreSame(expected, ok.Value);
        host.Inventory.Verify(service => service.ReconcileAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ReconcileAsync_MapsDaemonFailureToBadGatewayAsync()
    {
        await using ControllerTestHost host = await ControllerTestHost.CreateAsync(TestContext.CancellationToken);
        host.Inventory
            .Setup(service => service.ReconcileAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UfwIpcException(StatusCodes.Status500InternalServerError, "daemon enumeration failed"));

        IActionResult result = await host.Controller.ReconcileAsync(TestContext.CancellationToken);

        ObjectResult problem = Assert.IsInstanceOfType<ObjectResult>(result);
        Assert.AreEqual(StatusCodes.Status502BadGateway, problem.StatusCode);
    }

    private sealed class ControllerTestHost : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _services;
        private readonly AsyncServiceScope _scope;

        private ControllerTestHost(
            SqliteConnection connection,
            ServiceProvider services,
            AsyncServiceScope scope,
            Mock<INetworkInterfaceInventoryService> inventory,
            NetworkInterfacesController controller)
        {
            _connection = connection;
            _services = services;
            _scope = scope;
            Inventory = inventory;
            Controller = controller;
        }

        public Mock<INetworkInterfaceInventoryService> Inventory { get; }

        public NetworkInterfacesController Controller { get; }

        public static async Task<ControllerTestHost> CreateAsync(CancellationToken cancellationToken)
        {
            SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync(cancellationToken);

            ServiceCollection services = new();
            services.AddLogging();
            services.AddProblemDetails();
            services.AddControllers();
            services.AddSingleton<IModelLoader, ApplicationModelLoader>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
            services.AddTransactionManagement<ApplicationDbContext>(options =>
                options.UseIsolationLevel(IsolationLevel.ReadCommitted));

            ServiceProvider serviceProvider = services.BuildServiceProvider();
            AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.Database.EnsureCreatedAsync(cancellationToken);

            Mock<INetworkInterfaceInventoryService> inventory = new();
            ITransactionServiceHandle transactionService = scope.ServiceProvider.GetRequiredService<ITransactionServiceHandle>();
            NetworkInterfacesController controller = new(inventory.Object, transactionService)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        RequestServices = scope.ServiceProvider,
                    }
                }
            };

            return new ControllerTestHost(connection, serviceProvider, scope, inventory, controller);
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _services.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
