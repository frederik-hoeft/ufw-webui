using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Diagnostics;
using Ufw.Web.Security;

namespace Ufw.Web.Tests.Security;

[TestClass]
public sealed class AntiforgeryValidationMiddlewareTests
{
    [TestMethod]
    public async Task InvokeAsync_WithoutRequirement_SkipsValidationAsync()
    {
        Mock<IAntiforgery> antiforgery = new();
        bool nextInvoked = false;
        AntiforgeryValidationMiddleware middleware = CreateMiddleware(antiforgery.Object, _ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });
        DefaultHttpContext context = new();

        await middleware.InvokeAsync(context);

        Assert.IsTrue(nextInvoked);
        antiforgery.Verify(service => service.ValidateRequestAsync(It.IsAny<HttpContext>()), Times.Never);
    }

    [TestMethod]
    public async Task InvokeAsync_WithRequirementAndValidToken_InvokesNextAsync()
    {
        Mock<IAntiforgery> antiforgery = new();
        antiforgery.Setup(service => service.ValidateRequestAsync(It.IsAny<HttpContext>())).Returns(Task.CompletedTask);
        bool nextInvoked = false;
        AntiforgeryValidationMiddleware middleware = CreateMiddleware(antiforgery.Object, _ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });
        DefaultHttpContext context = CreateProtectedContext();

        await middleware.InvokeAsync(context);

        Assert.IsTrue(nextInvoked);
        antiforgery.Verify(service => service.ValidateRequestAsync(context), Times.Once);
    }

    [TestMethod]
    public async Task InvokeAsync_WithRequirementAndInvalidToken_ReturnsBadRequestAsync()
    {
        Mock<IAntiforgery> antiforgery = new();
        antiforgery
            .Setup(service => service.ValidateRequestAsync(It.IsAny<HttpContext>()))
            .ThrowsAsync(new AntiforgeryValidationException("Invalid antiforgery token."));
        bool nextInvoked = false;
        AntiforgeryValidationMiddleware middleware = CreateMiddleware(antiforgery.Object, _ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });
        DefaultHttpContext context = CreateProtectedContext();

        await middleware.InvokeAsync(context);

        Assert.IsFalse(nextInvoked);
        Assert.AreEqual(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        antiforgery.Verify(service => service.ValidateRequestAsync(context), Times.Once);
    }

    [TestMethod]
    public async Task InvokeAsync_WithProtectedEndpoint_DoesNotRequireFrameworkAntiforgeryMiddlewareAsync()
    {
        Mock<IAntiforgery> antiforgery = new();
        antiforgery.Setup(service => service.ValidateRequestAsync(It.IsAny<HttpContext>())).Returns(Task.CompletedTask);
        ServiceCollection services = new();
        services.AddLogging();
        services.AddRouting();
        services.AddSingleton(antiforgery.Object);
        using DiagnosticListener diagnosticListener = new("Ufw.Web.Tests");
        services.AddSingleton(diagnosticListener);
        services.AddSingleton<DiagnosticSource>(diagnosticListener);
        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        ApplicationBuilder app = new(serviceProvider);
        app.UseRouting();
        app.UseMiddleware<AntiforgeryValidationMiddleware>();
        app.UseEndpoints(endpoints => endpoints.MapPost("/protected", static context => context.Response.WriteAsync("ok"))
            .WithMetadata(new RequireAntiforgeryValidationAttribute()));
        RequestDelegate pipeline = app.Build();
        DefaultHttpContext context = new()
        {
            RequestServices = serviceProvider,
        };
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/protected";
        context.Response.Body = new MemoryStream();

        await pipeline(context);

        Assert.AreEqual(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.AreEqual("ok", await ReadResponseBodyAsync(context.Response));
        antiforgery.Verify(service => service.ValidateRequestAsync(context), Times.Once);
    }

    private static AntiforgeryValidationMiddleware CreateMiddleware(IAntiforgery antiforgery, RequestDelegate next) =>
        new(next, antiforgery, NullLogger<AntiforgeryValidationMiddleware>.Instance);

    private static async Task<string> ReadResponseBodyAsync(HttpResponse response)
    {
        response.Body.Position = 0;
        using StreamReader reader = new(response.Body, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private static DefaultHttpContext CreateProtectedContext()
    {
        DefaultHttpContext context = new();
        context.SetEndpoint(new Endpoint(
            static _ => Task.CompletedTask,
            new EndpointMetadataCollection(new RequireAntiforgeryValidationAttribute()),
            "antiforgery-test"));
        return context;
    }
}
