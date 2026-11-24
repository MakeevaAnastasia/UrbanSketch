using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UrbanSketch.Financial.Api.Data;
using UrbanSketch.Financial.Api.Endpoints;
using UrbanSketch.Financial.Api.Models;

internal enum TestMode { Normal, EmptyProjects, EmptyVariants, DatabaseFailure }

internal sealed class ApiTestHost(WebApplication app, HttpClient client) : IAsyncDisposable
{
    public HttpClient Client { get; } = client;

    public static async Task<ApiTestHost> StartAsync(string readOnlyConnection, TestMode mode = TestMode.Normal,
        Func<FinancialDbContext>? contextFactory = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseSetting("preventHostingStartup", "true");
        builder.Services.AddScoped<FinancialDbContext>(_ =>
        {
            if (contextFactory != null) return contextFactory();
            if (mode == TestMode.DatabaseFailure)
                throw new InvalidOperationException("PRIVATE_DATABASE_DIAGNOSTIC_SENTINEL");
            var options = new DbContextOptionsBuilder<FinancialDbContext>().UseNpgsql(readOnlyConnection).Options;
            return mode switch
            {
                TestMode.EmptyProjects => new EmptyProjectsContext(options),
                TestMode.EmptyVariants => new EmptyVariantsContext(options),
                _ => new FinancialDbContext(options)
            };
        });
        var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.UseApiErrorHandling();
        app.MapProjectsEndpoints();
        app.MapConceptVariantsEndpoints();
        app.MapFinancialModelsEndpoints();
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new ApiTestHost(app, new HttpClient { BaseAddress = new Uri(address) });
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await app.StopAsync();
        await app.DisposeAsync();
    }

    private sealed class EmptyProjectsContext(DbContextOptions<FinancialDbContext> options) : FinancialDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Project>().HasQueryFilter(_ => false);
        }
    }

    private sealed class EmptyVariantsContext(DbContextOptions<FinancialDbContext> options) : FinancialDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<ConceptVariant>().HasQueryFilter(_ => false);
        }
    }
}
